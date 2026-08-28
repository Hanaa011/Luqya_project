using System;
using System.Linq;
using System.Linq.Dynamic.Core;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.BlobStoring;
using LostFound.AI.AiService;
using LostFound.Reports.Dtos;
using LostFound.Reporters;
using LostFound.BackgroundJobs;

namespace LostFound.Reports
{
    // CRUD only. No CategoryId/Color anywhere - AI fills those in
    // asynchronously after Create/Update via the background job.
    public class ReportAppService : ApplicationService, IReportAppService
    {
        private readonly IReportRepository _reportRepository;
        private readonly ReporterManager _reporterManager;
        private readonly IBackgroundJobManager _backgroundJobManager;
        private readonly IBlobContainer<ReportImageContainer> _imageContainer;
        private readonly IImageValidator _imageValidator;
        private readonly IAiServiceClient _aiServiceClient;
        private readonly ILogger<ReportAppService> _logger;

        public ReportAppService(
            IReportRepository reportRepository,
            ReporterManager reporterManager,
            IBackgroundJobManager backgroundJobManager,
            IBlobContainer<ReportImageContainer> imageContainer,
            IImageValidator imageValidator,
            IAiServiceClient aiServiceClient,
            ILogger<ReportAppService> logger)
        {
            _reportRepository = reportRepository;
            _reporterManager = reporterManager;
            _backgroundJobManager = backgroundJobManager;
            _imageContainer = imageContainer;
            _imageValidator = imageValidator;
            _aiServiceClient = aiServiceClient;
            _logger = logger;
        }

        // PHASE-VALIDATION-08: see IReportAppService.UploadImageAsync.
        // GUID-named blob (no client-supplied file name/extension trusted)
        // - ReportMatchingBackgroundJob only reads the bytes back by this
        // exact name, it never inspects the name itself, so a fixed
        // extension-less name is sufficient and avoids path-injection
        // concerns from a client-supplied file name.
        //
        // Task A2 (Luqya-System-Reference.md §20/§38 Issue #15): size/format
        // validation now goes through the shared IImageValidator so this
        // path and the AI image-search path (AiSearchAppService.SearchAsync)
        // can never drift on what counts as a valid image. A rejected image
        // surfaces as a clean UserFriendlyException, not a downstream blob-
        // storage or AI-provider error.
        public async Task<string> UploadImageAsync(byte[] imageBytes)
        {
            var validation = _imageValidator.Validate(imageBytes);
            if (!validation.IsValid)
            {
                throw new UserFriendlyException(validation.ErrorMessage!);
            }

            var blobName = GuidGenerator.Create().ToString("N");
            await _imageContainer.SaveAsync(blobName, imageBytes);
            return blobName;
        }

        public async Task<ReportDto> GetAsync(Guid id)
        {
            var report = await _reportRepository.GetAsync(id);
            return MapToDto(report);
        }

        // Diagnosed root cause of the list endpoint scaling to 10+ seconds as
        // row count grew (46 rows, otherwise-trivial query): the previous
        // version materialized full Report entities, which include
        // EmbeddingJson/MetadataEmbeddingJson/ImageEmbeddingJson - each a
        // serialized float[] (~25-30KB of JSON text per populated column,
        // up to 3 per row for an AI-classified report). MapToDto never reads
        // their VALUES, only HasEmbedding's null-check and GetAiTags()'s
        // (much smaller) AiTagsJson. SQL execution itself was consistently
        // ~70ms (confirmed via temporary EF Core command logging); the
        // remaining multi-second cost was fetching/materializing/GC-ing
        // those large unused blob columns for every row. Projecting to only
        // the columns ReportDto actually needs (below) makes EF Core
        // generate a SELECT that never reads those columns off the wire in
        // the first place - same rows, same DTO shape, same total/paging
        // semantics, no API contract change.
        public async Task<PagedResultDto<ReportDto>> GetListAsync(GetReportListDto input)
        {
            var queryable = await _reportRepository.GetQueryableAsync();

            queryable = queryable
                .WhereIf(!string.IsNullOrWhiteSpace(input.Filter), r =>
                    (r.Description != null && r.Description.Contains(input.Filter!)) ||
                    (r.LocationDetails != null && r.LocationDetails.Contains(input.Filter!)))
                .WhereIf(input.Type.HasValue, r => r.Type == input.Type!.Value)
                .WhereIf(input.Status.HasValue, r => r.Status == input.Status!.Value)
                .WhereIf(input.LocationId.HasValue, r => r.LocationId == input.LocationId!.Value)
                .WhereIf(input.ReporterId.HasValue, r => r.ReporterId == input.ReporterId!.Value)
                .WhereIf(input.CategoryId.HasValue, r => r.CategoryId == input.CategoryId!.Value);

            var totalCount = queryable.Count();
            var sorting = string.IsNullOrWhiteSpace(input.Sorting) ? "CreationTime desc" : input.Sorting;

            var rows = await AsyncExecuter.ToListAsync(
                queryable.OrderBy(sorting).Skip(input.SkipCount).Take(input.MaxResultCount)
                    .Select(r => new ReportListRow
                    {
                        Id = r.Id,
                        CreationTime = r.CreationTime,
                        CreatorId = r.CreatorId,
                        LastModificationTime = r.LastModificationTime,
                        LastModifierId = r.LastModifierId,
                        ReporterId = r.ReporterId,
                        CategoryId = r.CategoryId,
                        LocationId = r.LocationId,
                        LocationDetails = r.LocationDetails,
                        Type = r.Type,
                        Description = r.Description,
                        LostFoundDate = r.LostFoundDate,
                        ImagePath = r.ImagePath,
                        IsItemWithFinder = r.IsItemWithFinder,
                        PickupLocation = r.PickupLocation,
                        Status = r.Status,
                        HasEmbedding = r.EmbeddingJson != null && r.EmbeddingJson != "",
                        Color = r.Color,
                        AiObjectType = r.AiObjectType,
                        AiBrand = r.AiBrand,
                        AiTagsJson = r.AiTagsJson,
                        IsAiClassified = r.IsAiClassified,
                    })
            );

            return new PagedResultDto<ReportDto>(totalCount, rows.Select(MapRowToDto).ToList());
        }

        // Mirrors Report.GetAiTags()'s own null/blank-safe deserialize
        // (see Report.cs's private DeserializeJson<T>) - duplicated rather
        // than shared because it operates on the raw AiTagsJson string from
        // the projection above, not a live Report entity.
        private static System.Collections.Generic.List<string> DeserializeAiTags(string? aiTagsJson)
        {
            if (string.IsNullOrWhiteSpace(aiTagsJson))
            {
                return new System.Collections.Generic.List<string>();
            }

            return System.Text.Json.JsonSerializer.Deserialize<System.Collections.Generic.List<string>>(aiTagsJson)
                ?? new System.Collections.Generic.List<string>();
        }

        private static ReportDto MapRowToDto(ReportListRow row)
        {
            return new ReportDto
            {
                Id = row.Id,
                CreationTime = row.CreationTime,
                CreatorId = row.CreatorId,
                LastModificationTime = row.LastModificationTime,
                LastModifierId = row.LastModifierId,
                ReporterId = row.ReporterId,
                CategoryId = row.CategoryId,
                LocationId = row.LocationId,
                LocationDetails = row.LocationDetails,
                Type = row.Type,
                Description = row.Description,
                LostFoundDate = row.LostFoundDate,
                ImagePath = row.ImagePath,
                IsItemWithFinder = row.IsItemWithFinder,
                PickupLocation = row.PickupLocation,
                Status = row.Status,
                HasEmbedding = row.HasEmbedding,
                Color = row.Color,
                AiObjectType = row.AiObjectType,
                AiBrand = row.AiBrand,
                AiTags = DeserializeAiTags(row.AiTagsJson),
                IsAiClassified = row.IsAiClassified,
            };
        }

        // Column-level projection shape for GetListAsync - deliberately
        // excludes EmbeddingJson/MetadataEmbeddingJson/ImageEmbeddingJson
        // (see GetListAsync's remarks) so EF Core never selects them.
        private sealed class ReportListRow
        {
            public Guid Id { get; set; }
            public DateTime CreationTime { get; set; }
            public Guid? CreatorId { get; set; }
            public DateTime? LastModificationTime { get; set; }
            public Guid? LastModifierId { get; set; }
            public Guid ReporterId { get; set; }
            public Guid? CategoryId { get; set; }
            public Guid LocationId { get; set; }
            public string? LocationDetails { get; set; }
            public ReportType Type { get; set; }
            public string? Description { get; set; }
            public DateTime? LostFoundDate { get; set; }
            public string? ImagePath { get; set; }
            public bool IsItemWithFinder { get; set; }
            public string? PickupLocation { get; set; }
            public ReportStatus Status { get; set; }
            public bool HasEmbedding { get; set; }
            public string? Color { get; set; }
            public string? AiObjectType { get; set; }
            public string? AiBrand { get; set; }
            public string? AiTagsJson { get; set; }
            public bool IsAiClassified { get; set; }
        }

        // Reuses ai_service's analysis-only endpoint (IAiServiceClient.AnalyzeImageAsync
        // -> POST /api/ai/analyze-image, no matching call) to enrich a found-item
        // report's free-text Description synchronously at creation time. This is
        // additive to, not a replacement for, ReportMatchingBackgroundJob's own
        // async classification (which still runs afterward and fills the
        // separate AiObjectType/Color/AiBrand fields via the existing provider
        // chain - CreateReportDto has no slot for those, only Description).
        // Best-effort: any failure here is logged and swallowed so report
        // creation is never blocked by ai_service being unavailable.
        private async Task<string?> BuildFoundItemDescriptionAsync(ReportType type, string? imagePath, string? description)
        {
            if (type != ReportType.Found || string.IsNullOrWhiteSpace(imagePath))
            {
                return description;
            }

            try
            {
                if (!await _imageContainer.ExistsAsync(imagePath))
                {
                    return description;
                }

                var imageBytes = await _imageContainer.GetAllBytesAsync(imagePath);
                var mimeType = ImageMimeTypeResolver.Resolve(imageBytes);

                var analysis = await _aiServiceClient.AnalyzeImageAsync(imageBytes, mimeType);

                if (string.IsNullOrWhiteSpace(analysis.Description))
                {
                    return description;
                }

                return string.IsNullOrWhiteSpace(description)
                    ? analysis.Description
                    : $"{description}. {analysis.Description}";
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "ai_service image analysis failed for a found-item report; continuing with the user-provided description only.");
                return description;
            }
        }

        // Workflow: Save Report (no Category) -> Queue Background Job -> AI
        // Classification -> Embeddings -> Matching -> Notification.
        public async Task<ReportDto> CreateAsync(CreateReportDto input)
        {
            Reporter reporter;

            if (CurrentUser.IsAuthenticated)
            {
                reporter = await _reporterManager.FindOrCreateForIdentityUserAsync(
                    CurrentUser.Id!.Value,
                    input.ReporterName ?? CurrentUser.Name,
                    input.ReporterPhone ?? CurrentUser.PhoneNumber ?? string.Empty,
                    input.ReporterEmail ?? CurrentUser.Email,
                    input.PreferredContact
                );
            }
            else
            {
                if (string.IsNullOrWhiteSpace(input.ReporterPhone))
                {
                    throw new BusinessException(ReporterErrorCodes.PhoneIsRequiredForGuests);
                }

                reporter = await _reporterManager.FindOrCreateForGuestAsync(
                    input.ReporterName,
                    input.ReporterPhone,
                    input.ReporterEmail,
                    input.PreferredContact
                );
            }

            var description = await BuildFoundItemDescriptionAsync(input.Type, input.ImagePath, input.Description);

            var report = new Report(
                GuidGenerator.Create(),
                reporter.Id,
                input.LocationId,
                input.Type,
                description,
                input.LocationDetails,
                input.LostFoundDate,
                input.ImagePath,
                input.IsItemWithFinder,
                input.PickupLocation
            );

            await _reportRepository.InsertAsync(report);

            await _backgroundJobManager.EnqueueAsync(new ReportMatchingBackgroundJobArgs { ReportId = report.Id });

            return MapToDto(report);
        }

        public async Task<ReportDto> UpdateAsync(Guid id, UpdateReportDto input)
        {
            var report = await _reportRepository.GetAsync(id);
            var previousDescription = report.Description;
            var previousImagePath = report.ImagePath;
            var previousLocationDetails = report.LocationDetails;

            report.SetStatus(input.Status);
            report.UpdateDetails(
                input.Description,
                input.LocationDetails,
                input.LostFoundDate,
                input.ImagePath,
                input.IsItemWithFinder,
                input.PickupLocation
            );

            await _reportRepository.UpdateAsync(report);

            if (previousDescription != input.Description ||
                previousImagePath != input.ImagePath ||
                previousLocationDetails != input.LocationDetails)
            {
                await _backgroundJobManager.EnqueueAsync(new ReportMatchingBackgroundJobArgs { ReportId = report.Id });
            }

            return MapToDto(report);
        }

        public async Task DeleteAsync(Guid id)
        {
            await _reportRepository.DeleteAsync(id);
        }

        // Report mapping is manual (not via Mapperly) because of the AI-only
        // derived fields (AiTags via GetAiTags(), HasEmbedding, etc.).
        private static ReportDto MapToDto(Report report)
        {
            return new ReportDto
            {
                Id = report.Id,
                CreationTime = report.CreationTime,
                CreatorId = report.CreatorId,
                LastModificationTime = report.LastModificationTime,
                LastModifierId = report.LastModifierId,
                ReporterId = report.ReporterId,
                CategoryId = report.CategoryId,
                LocationId = report.LocationId,
                LocationDetails = report.LocationDetails,
                Type = report.Type,
                Description = report.Description,
                LostFoundDate = report.LostFoundDate,
                ImagePath = report.ImagePath,
                IsItemWithFinder = report.IsItemWithFinder,
                PickupLocation = report.PickupLocation,
                Status = report.Status,
                HasEmbedding = report.HasEmbedding,
                Color = report.Color,
                AiObjectType = report.AiObjectType,
                AiBrand = report.AiBrand,
                AiTags = report.GetAiTags(),
                IsAiClassified = report.IsAiClassified
            };
        }
    }
}
