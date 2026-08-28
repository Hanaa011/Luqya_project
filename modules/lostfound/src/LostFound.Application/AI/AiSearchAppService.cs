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

        public async Task<AiSearchResponseDto> SearchAsync(AiSearchInputDto input)
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

            AiSearchResponseDto? response = null;

            try
            {
                _logger.LogInformation("Calling ai_service (primary) via IAiServiceClient...");
                response = await SearchWithAiServiceAsync(input, imageBytes);
                _logger.LogInformation(
                    "ai_service returned ShouldMatch={ShouldMatch}, {Count} result(s) after enrichment/filtering.",
                    response.ShouldMatch, response.Results.Count);
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

            if (response == null)
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

                // The fallback has no conversational layer - it always
                // searched, so ShouldMatch is unconditionally true and there
                // is no Reply/ExtractedItem/FollowUpPrompt to carry.
                response = new AiSearchResponseDto
                {
                    ShouldMatch = true,
                    Results = results.Select(r => new AiSearchResultDto
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
                    }).ToList()
                };
            }

            foreach (var result in response.Results)
            {
                _logger.LogInformation(
                    "ReportId: {Id} | Score: {Score}% | Description: {Description}",
                    result.ReportId,
                    result.ScorePercentage,
                    result.Description);
            }

            _logger.LogInformation("Returning {Count} result(s), ShouldMatch={ShouldMatch}.", response.Results.Count, response.ShouldMatch);
            _logger.LogInformation("========== SearchAsync Finished ==========");

            return response;
        }

        /// <summary>
        /// Primary search path. ai_service always searches ONE direction per
        /// call (candidates opposite the query's own kind) - when
        /// <paramref name="input"/>.Type doesn't pin a single direction, both
        /// directions are queried concurrently and merged, so "search
        /// everything" still covers Lost and Found candidates exactly like
        /// the existing implementation does. Reply/ShouldMatch/ExtractedItem/
        /// FollowUpPrompt are direction-independent (same message/image
        /// produced them), so only the first direction's copy is kept; only
        /// the match lists themselves are combined.
        /// </summary>
        private async Task<AiSearchResponseDto> SearchWithAiServiceAsync(AiSearchInputDto input, byte[]? imageBytes)
        {
            var searcherIsFinderDirections = input.Type switch
            {
                ReportType.Found => new[] { false }, // searcher lost it -> candidates are Found reports
                ReportType.Lost => new[] { true },   // searcher found it -> candidates are Lost reports
                _ => new[] { false, true },
            };

            var knownContext = (input.ContextType, input.ContextDescription, input.ContextColor, input.ContextLocation, input.ContextReportKind, input.ContextItemNameLocal);

            var directionTasks = searcherIsFinderDirections
                .Select(searcherIsFinder => imageBytes != null
                    ? _aiServiceClient.SearchImageAsync(
                        imageBytes, ImageMimeTypeResolver.Resolve(imageBytes), input.Text, locationName: null, searcherIsFinder, knownContext)
                    : _aiServiceClient.SearchTextAsync(input.Text!, locationName: null, searcherIsFinder, knownContext))
                .ToArray();

            var directionResults = await Task.WhenAll(directionTasks);
            var primary = directionResults[0];

            var response = new AiSearchResponseDto
            {
                Reply = primary.Reply,
                ShouldMatch = primary.ShouldMatch,
                ExtractedType = primary.ExtractedType,
                ExtractedDescription = primary.ExtractedDescription,
                ExtractedColor = primary.ExtractedColor,
                ExtractedLocation = primary.ExtractedLocation,
                ReportKind = primary.ReportKind,
                ItemNameLocal = primary.ItemNameLocal,
                FollowUpPrompt = primary.FollowUpPrompt,
            };

            if (!primary.ShouldMatch)
            {
                // Reply-only turn (greeting / incomplete description) - never
                // fetch candidates or run matching for it.
                return response;
            }

            // Root-caused de-dup: now that report_kind self-correction can
            // resolve BOTH directions to the same actual direction (the
            // message clearly said "lost" or "found", overriding whichever
            // direction each concurrent call originally started as - see
            // ai_service's chat_search), those two calls' candidate pools
            // are no longer guaranteed distinct, and a real report can come
            // back from both, verbatim-identical, as two separate result
            // entries. Keyed on whichever of FoundReportId/LostReportId is
            // actually populated for that match (never both), keeping the
            // higher-scoring copy on the rare chance the two direction
            // calls' scores ever differ.
            var allMatches = directionResults
                .SelectMany(r => r.Matches)
                .GroupBy(m => !string.IsNullOrWhiteSpace(m.FoundReportId) ? m.FoundReportId : m.LostReportId)
                .Select(g => g.OrderByDescending(m => m.SimilarityScore).First())
                .ToList();
            response.Results = await EnrichAsync(allMatches, input.MaxResults);

            return response;
        }

        /// <summary>
        /// ai_service's match results only carry the candidate's report id,
        /// score, and reason (see AiServiceMatch) - not its Description/Color/
        /// AiObjectType/ImagePath. This looks each candidate up by id, fetched
        /// by exactly those ids (see GetByIdsAsync) rather than every report,
        /// regardless of type. Root-cause fix (type filter): this used to
        /// filter the lookup pool by input.Type, on the assumption that a
        /// match's actual candidate type always agrees with the caller's
        /// requested Type. That assumption breaks once natural-language
        /// intent (see chat_search's report_kind self-correction) flips the
        /// real search direction away from a caller-selected pill/Type that
        /// contradicts the message - ai_service then correctly returns a
        /// candidate of the OTHER type, but the old type-filtered lookup
        /// here couldn't find it by id and silently dropped the match
        /// (reportsById.TryGetValue failing below). The type filter added no
        /// real correctness here in the first place - ai_service's own
        /// is_item_with_finder filtering already guarantees every candidate
        /// id it returns belongs to the right pool - so removing it only
        /// removes a source of false negatives, never widens what can match.
        /// </summary>
        private async Task<List<AiSearchResultDto>> EnrichAsync(
            List<AiServiceMatch> matches, int maxResults)
        {
            if (matches.Count == 0)
            {
                return new List<AiSearchResultDto>();
            }

            // Root-cause latency fix: this used to fetch EVERY report via
            // GetReportsByTypeAsync(null) just to look up the handful that
            // actually matched - the same "full entity, every row" cost
            // (each row carries EmbeddingJson/MetadataEmbeddingJson/
            // ImageEmbeddingJson, ~25-30KB of unused JSON per populated
            // column) that made ReportAppService.GetListAsync take 10+
            // seconds, in a second, independent code path. matches already
            // names exactly which reports are needed, so fetch only those.
            var candidateIds = matches
                .Select(m => !string.IsNullOrWhiteSpace(m.FoundReportId) ? m.FoundReportId : m.LostReportId)
                .Select(idText => Guid.TryParse(idText, out var id) ? id : (Guid?)null)
                .Where(id => id.HasValue)
                .Select(id => id!.Value)
                .Distinct()
                .ToList();

            var candidateReports = await _reportRepository.GetByIdsAsync(candidateIds);
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
