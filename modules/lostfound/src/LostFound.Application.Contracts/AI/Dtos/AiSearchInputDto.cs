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

        // "lost" or "found" - a direction already confirmed by an earlier
        // message in this conversation (e.g. "وجدت قلادة حمراء"), echoed
        // back from the previous turn's AiSearchResponseDto.ReportKind.
        // Without this, a later turn that only adds a location/color (no
        // verb, e.g. "في المول") would lose that correction and silently
        // fall back to whatever direction the caller's selected pill
        // implies - see AiSearchAppService/ai_service's chat_search.
        public string? ContextReportKind { get; set; }

        // The item's name in the ORIGINAL language the searcher wrote in
        // (e.g. "الشماغ"), echoed back from AiSearchResponseDto.
        // ItemNameLocal. Search-quality fix, not just wording: a candidate
        // report's own description is often bilingual (an original-language
        // item mention plus an English AI-generated visual description),
        // so an English-only query measurably under-scores a real match
        // against it - see ai_service's matching_service.semantic_text.
        // Carried verbatim across turns (never re-derived from just the
        // English type) because re-deriving it is not reliably the same
        // word twice.
        public string? ContextItemNameLocal { get; set; }
    }
}