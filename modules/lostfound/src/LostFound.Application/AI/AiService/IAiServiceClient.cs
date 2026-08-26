using System.Collections.Generic;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace LostFound.AI.AiService
{
    // Thin HTTP client for the separate Python ai_service - the PRIMARY
    // search/matching implementation per AiSearchAppService. Every method
    // here throws on a genuine technical failure (HTTP error, timeout,
    // connection failure, malformed response) so the caller can tell that
    // apart from a valid "no matches" result and decide whether to fall
    // back to the existing AiMatchingService.
    public interface IAiServiceClient
    {
        // isFinder: false = the searcher lost an item (candidates are found
        // reports), true = the searcher found an item (candidates are lost
        // reports) - maps directly to ai_service's report_kind form field.
        // knownContext: the previous turn's extracted fields, echoed back so
        // ai_service can combine them with this message/image in one call -
        // each element is a single concise current value, never history.
        Task<AiServiceSearchResult> SearchTextAsync(
            string text, string? locationName, bool isFinder,
            (string? Type, string? Description, string? Color, string? Location) knownContext,
            CancellationToken cancellationToken = default);

        Task<AiServiceSearchResult> SearchImageAsync(
            byte[] imageBytes, string mimeType, string? text, string? locationName, bool isFinder,
            (string? Type, string? Description, string? Color, string? Location) knownContext,
            CancellationToken cancellationToken = default);

        // Analysis only - never calls ai_service's matching logic.
        Task<AiImageAnalysisResult> AnalyzeImageAsync(
            byte[] imageBytes, string mimeType, CancellationToken cancellationToken = default);
    }

    public class AiServiceMatch
    {
        [JsonPropertyName("lost_report_id")]
        public string? LostReportId { get; set; }

        [JsonPropertyName("found_report_id")]
        public string? FoundReportId { get; set; }

        [JsonPropertyName("similarity_score")]
        public double SimilarityScore { get; set; }

        [JsonPropertyName("match_reason")]
        public string? MatchReason { get; set; }
    }

    // Conversational metadata alongside matches - Reply/ShouldMatch let the
    // caller distinguish a reply-only turn (greeting, incomplete
    // description) from a turn that actually searched; ExtractedX is the
    // compact "known so far" state the caller echoes back as the next
    // call's knownContext. FollowUpPrompt may be set together with a
    // non-empty Matches list - it never gates or blocks them.
    public class AiServiceSearchResult
    {
        public string? Reply { get; set; }
        public bool ShouldMatch { get; set; }
        public string? ExtractedType { get; set; }
        public string? ExtractedDescription { get; set; }
        public string? ExtractedColor { get; set; }
        public string? ExtractedLocation { get; set; }
        public string? FollowUpPrompt { get; set; }
        public List<AiServiceMatch> Matches { get; set; } = new();
    }

    public class AiImageAnalysisResult
    {
        [JsonPropertyName("type")]
        public string? Type { get; set; }

        [JsonPropertyName("description")]
        public string? Description { get; set; }

        [JsonPropertyName("color")]
        public string? Color { get; set; }
    }
}
