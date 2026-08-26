using System.Collections.Generic;

namespace LostFound.AI.Dtos
{
    public class AiSearchResponseDto
    {
        // Conversational reply text, or null when the search path (fallback
        // AiMatchingService) has no conversational layer.
        public string? Reply { get; set; }

        // false = a reply-only turn (greeting, incomplete description) -
        // Results is always empty and no candidate fetch/matching ran.
        public bool ShouldMatch { get; set; }

        // Compact "known so far" state - echo these back as the next
        // request's AiSearchInputDto.ContextX fields. Each is a single
        // concise current value, never an accumulated history.
        public string? ExtractedType { get; set; }
        public string? ExtractedDescription { get; set; }
        public string? ExtractedColor { get; set; }
        public string? ExtractedLocation { get; set; }

        // Optional refinement nudge (e.g. "add a location to improve
        // results"). May be present together with a non-empty Results -
        // it never gates or blocks valid results.
        public string? FollowUpPrompt { get; set; }

        public List<AiSearchResultDto> Results { get; set; } = new();
    }
}
