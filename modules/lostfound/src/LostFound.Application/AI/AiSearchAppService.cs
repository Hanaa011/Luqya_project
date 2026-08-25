using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Volo.Abp;
using Volo.Abp.Application.Services;
using LostFound.AI.AiService;
using LostFound.AI.Dtos;
using LostFound.Reports;

namespace LostFound.AI
{
    public class AiSearchAppService : ApplicationService, IAiSearchAppService
    {
        /// <summary>
        /// Phase 4 Part 1: hard confidence floor enforced server-side on every
        /// search result, for every query shape (text-only, image-only, and
        /// combined) - all three flow through this single method. Comparison
        /// is inclusive (&gt;= 55), and this floor is applied unconditionally;
        /// it does not depend on (and is never relaxed by) whatever the caller
        /// sends via <see cref="AiSearchInputDto.MinimumScorePercentage"/>, so
        /// the frontend can never cause a sub-55% result to be returned.
        /// Applied identically regardless of which implementation served the
        /// search (ai_service or the fallback), see ApplyConfidenceFloor.
        /// </summary>
        private const double MinimumConfidenceFloorPercentage = 55.0;

        private readonly IAiMatchingService _aiMatchingService;
        private readonly IAiServiceClient _aiServiceClient;
        private readonly IReportRepository _reportRepository;
        private readonly IImageValidator _imageValidator;
        private readonly ILogger<AiSearchAppService> _logger;

        public AiSearchAppService(
            IAiMatchingService aiMatchingService,
            IAiServiceClient aiServiceClient,
            IReportRepository reportRepository,
            IImageValidator imageValidator,
            ILogger<AiSearchAppService> logger)
        {
            _aiMatchingService = aiMatchingService;
            _aiServiceClient = aiServiceClient;
            _reportRepository = reportRepository;
            _imageValidator = imageValidator;
            _logger = logger;

            _logger.LogInformation("========== AiSearchAppService Created ==========");
        }

        public async Task<List<AiSearchResultDto>> SearchAsync(AiSearchInputDto input)
        {
            _logger.LogInformation("========== SearchAsync Started ==========");

            _logger.LogInformation("Input Text: {Text}", input.Text);
            _logger.LogInformation("Has Image: {HasImage}", !string.IsNullOrWhiteSpace(input.ImageBase64));
            _logger.LogInformation("Type: {Type}", input.Type);
            _logger.LogInformation("Max Results: {MaxResults}", input.MaxResults);
            _logger.LogInformation("Minimum Score: {MinimumScore}", input.MinimumScorePercentage);

            if (string.IsNullOrWhiteSpace(input.Text) &&
                string.IsNullOrWhiteSpace(input.ImageBase64))
            {
                _logger.LogWarning("Search rejected because no text or image was provided.");

                throw new UserFriendlyException(
                    "Provide a description, an image, or both to search.");
            }

            byte[]? imageBytes = null;

            if (!string.IsNullOrWhiteSpace(input.ImageBase64))
            {
                imageBytes = Convert.FromBase64String(input.ImageBase64);
                _logger.LogInformation("Image Size: {Size} bytes", imageBytes.Length);

                // Task A2 (Luqya-System-Reference.md §20/§38 Issue #15): same
                // shared validator as ReportAppService.UploadImageAsync - an
                // oversized or unrecognized-format search image is rejected
                // here, up front, with a clear validation error, rather than
                // reaching an AI provider and failing downstream.
                var validation = _imageValidator.Validate(imageBytes);
                if (!validation.IsValid)
                {
                    _logger.LogWarning("Search image rejected: {Reason}", validation.ErrorMessage);
                    throw new UserFriendlyException(validation.ErrorMessage!);
                }
            }

            List<AiSearchResultDto>? dto = null;

            try
            {
                _logger.LogInformation("Calling ai_service (primary) via IAiServiceClient...");
                dto = await SearchWithAiServiceAsync(input.Text, imageBytes, input.Type, input.MaxResults);
                _logger.LogInformation("ai_service returned {Count} result(s) after enrichment/filtering.", dto.Count);
            }
            catch (UserFriendlyException)
            {
                // A deliberate validation rejection, not a technical failure -
                // must reach the caller as-is, never hidden behind a fallback.
                throw;
            }
            catch (Exception ex)
            {
                // Genuine technical failure (HTTP error, timeout, connection
                // failure, malformed response - see IAiServiceClient's own
                // remarks). A successful-but-empty ai_service result never
                // reaches this catch block, so "no matches found" is never
                // mistaken for a failure here.
                _logger.LogWarning(
                    ex,
                    "ai_service search failed; falling back to the existing AiMatchingService implementation.");
            }

            if (dto == null)
            {
                _logger.LogInformation("Calling IAiMatchingService.FindSimilarReportsAsync (fallback)...");

                var results = await _aiMatchingService.FindSimilarReportsAsync(
                    input.Text,
                    imageBytes,
                    input.Type,
                    5); //input.MaxResults

                _logger.LogInformation(
                    "IAiMatchingService returned {Count} result(s) before filtering.",
                    results.Count);

                results = ApplyConfidenceFloor(results);

                dto = results.Select(r => new AiSearchResultDto
                {
                    ReportId = r.ReportId,
                    Description = r.Description,
                    Color = r.Color,
                    AiObjectType = r.AiObjectType,
                    Type = r.Type,
                    ImagePath = r.ImagePath,
                    ScorePercentage = r.ScorePercentage,
                    MatchReasons = r.MatchReasons,
                    MatchExplanation = r.MatchExplanation
                }).ToList();
            }

            foreach (var result in dto)
            {
                _logger.LogInformation(
                    "ReportId: {Id} | Score: {Score}% | Description: {Description}",
                    result.ReportId,
                    result.ScorePercentage,
                    result.Description);
            }

            _logger.LogInformation("Returning {Count} DTO(s).", dto.Count);
            _logger.LogInformation("========== SearchAsync Finished ==========");

            return dto;
        }

        /// <summary>
        /// Primary search path. ai_service always searches ONE direction per
        /// call (candidates opposite the query's own kind) - when
        /// <paramref name="type"/> doesn't pin a single direction, both
        /// directions are queried and merged, so "search everything" still
        /// covers Lost and Found candidates exactly like the existing
        /// implementation does.
        /// </summary>
        private async Task<List<AiSearchResultDto>> SearchWithAiServiceAsync(
            string? text, byte[]? imageBytes, ReportType? type, int maxResults)
        {
            var searcherIsFinderDirections = type switch
            {
                ReportType.Found => new[] { false }, // searcher lost it -> candidates are Found reports
                ReportType.Lost => new[] { true },   // searcher found it -> candidates are Lost reports
                _ => new[] { false, true },
            };

            var matches = new List<AiServiceMatch>();

            foreach (var searcherIsFinder in searcherIsFinderDirections)
            {
                var directionMatches = imageBytes != null
                    ? await _aiServiceClient.SearchImageAsync(
                        imageBytes, ImageMimeTypeResolver.Resolve(imageBytes), text, locationName: null, searcherIsFinder)
                    : await _aiServiceClient.SearchTextAsync(text!, locationName: null, searcherIsFinder);

                matches.AddRange(directionMatches);
            }

            return await EnrichAsync(matches, type, maxResults);
        }

        /// <summary>
        /// ai_service's match results only carry the candidate's report id,
        /// score, and reason (see AiServiceMatch) - not its Description/Color/
        /// AiObjectType/ImagePath. This looks each candidate up against the
        /// same searchable-reports set AiMatchingService already reads from,
        /// so the response DTO is fully populated without ai_service ever
        /// needing to know about those fields itself.
        /// </summary>
        private async Task<List<AiSearchResultDto>> EnrichAsync(
            List<AiServiceMatch> matches, ReportType? type, int maxResults)
        {
            if (matches.Count == 0)
            {
                return new List<AiSearchResultDto>();
            }

            var candidateReports = await _reportRepository.GetSearchableReportsAsync(type);
            var reportsById = candidateReports.ToDictionary(r => r.Id);

            var dto = new List<AiSearchResultDto>();

            foreach (var match in matches)
            {
                var candidateIdText = !string.IsNullOrWhiteSpace(match.FoundReportId)
                    ? match.FoundReportId
                    : match.LostReportId;

                if (string.IsNullOrWhiteSpace(candidateIdText) ||
                    !Guid.TryParse(candidateIdText, out var candidateId) ||
                    !reportsById.TryGetValue(candidateId, out var report))
                {
                    continue;
                }

                dto.Add(new AiSearchResultDto
                {
                    ReportId = report.Id,
                    Description = report.Description,
                    Color = report.Color,
                    AiObjectType = report.AiObjectType,
                    Type = report.Type,
                    ImagePath = report.ImagePath,
                    ScorePercentage = match.SimilarityScore,
                    MatchReasons = string.IsNullOrWhiteSpace(match.MatchReason)
                        ? new List<string>()
                        : new List<string> { match.MatchReason! },
                    MatchExplanation = match.MatchReason
                });
            }

            dto = ApplyConfidenceFloor(dto);

            return dto
                .OrderByDescending(r => r.ScorePercentage)
                .Take(maxResults)
                .ToList();
        }

        /// <summary>
        /// See MinimumConfidenceFloorPercentage - always applied, regardless
        /// of which implementation produced the results.
        /// </summary>
        private static List<AiSearchResultDto> ApplyConfidenceFloor(List<AiSearchResultDto> results) =>
            results.Where(r => r.ScorePercentage >= MinimumConfidenceFloorPercentage).ToList();

        private static List<RankedReportResult> ApplyConfidenceFloor(List<RankedReportResult> results) =>
            results.Where(r => r.ScorePercentage >= MinimumConfidenceFloorPercentage).ToList();
    }
}
