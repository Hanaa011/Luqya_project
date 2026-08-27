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

        // "lost" or "found" - the direction this turn actually searched
        // with (natural-language intent already applied), or null when
        // still undetermined. Echo back as the next request's
        // AiSearchInputDto.ContextReportKind so a later turn that doesn't
        // restate the user's role keeps using this one.
        public string? ReportKind { get; set; }

        // The item's name in the language the searcher wrote in (e.g.
        // "الشماغ"). Echo back as the next request's
        // AiSearchInputDto.ContextItemNameLocal - see that field's remarks.
        public string? ItemNameLocal { get; set; }

        // Optional refinement nudge (e.g. "add a location to improve
        // results"). May be present together with a non-empty Results -
        // it never gates or blocks valid results.
        public string? FollowUpPrompt { get; set; }

        public List<AiSearchResultDto> Results { get; set; } = new();
    }
}
