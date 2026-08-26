using LostFound.Reports;

namespace LostFound.AI.Dtos
{
    public class AiSearchInputDto
    {
        // At least one of Text / ImageBase64 must be provided - validated
        // in AiSearchAppService.
        public string? Text { get; set; }

        // Base64-encoded image bytes for image-based search.
        public string? ImageBase64 { get; set; }

        // Usually the OPPOSITE of what the user lost/found.
        public ReportType? Type { get; set; }

        public int MaxResults { get; set; } = 10;

        // Minimum similarity percentage (0-100)
        public double? MinimumScorePercentage { get; set; }

        // Conversation continuity - client echoes back the previous turn's
        // AiSearchResponseDto.ExtractedX fields here so the next message/
        // image can be combined with what's already known, without any
        // server-side session. Each field is a single concise current
        // value, never an accumulated/concatenated history.
        public string? ContextType { get; set; }
        public string? ContextDescription { get; set; }
        public string? ContextColor { get; set; }
        public string? ContextLocation { get; set; }
    }
}