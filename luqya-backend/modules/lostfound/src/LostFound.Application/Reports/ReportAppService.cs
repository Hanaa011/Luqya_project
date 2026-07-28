using System;
using System.Linq;
using System.Linq.Dynamic.Core;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.BackgroundJobs;
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

        public ReportAppService(
            IReportRepository reportRepository,
            ReporterManager reporterManager,
            IBackgroundJobManager backgroundJobManager)
        {
            _reportRepository = reportRepository;
            _reporterManager = reporterManager;
            _backgroundJobManager = backgroundJobManager;
        }

        public async Task<ReportDto> GetAsync(Guid id)
        {
            var report = await _reportRepository.GetAsync(id);
            return MapToDto(report);
        }

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

            var reports = await AsyncExecuter.ToListAsync(
                queryable.OrderBy(sorting).Skip(input.SkipCount).Take(input.MaxResultCount)
            );

            return new PagedResultDto<ReportDto>(totalCount, reports.Select(MapToDto).ToList());
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

            var report = new Report(
                GuidGenerator.Create(),
                reporter.Id,
                input.LocationId,
                input.Type,
                input.Description,
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
                LastModificationTime = report.LastModificationTime,
                CreatorId = report.CreatorId,
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
