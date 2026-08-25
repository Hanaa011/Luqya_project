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
        Task<List<AiServiceMatch>> SearchTextAsync(
            string text, string? locationName, bool isFinder, CancellationToken cancellationToken = default);

        Task<List<AiServiceMatch>> SearchImageAsync(
            byte[] imageBytes, string mimeType, string? text, string? locationName, bool isFinder,
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
