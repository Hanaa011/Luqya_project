using System;

namespace LostFound.Matches.Dtos
{
    // Phase 4 Part 3: the request shape for MatchAppService.ClaimAsync -
    // claiming a specific Smart Search result against one of the caller's
    // own reports, as either "this is my item" (IsMine = true) or "not my
    // item" (IsMine = false).
    public class ClaimMatchDto
    {
        // The report the user found via Smart Search and is claiming/dismissing.
        public Guid SearchResultReportId { get; set; }

        // The caller's own report this claim relates to - the caller must
        // own this report; enforced server-side, not trusted from the client.
        public Guid OwnReportId { get; set; }

        // The exact ScorePercentage the user was shown for this result at
        // claim time (decision #1) - stored verbatim on a newly-created
        // Match, never recomputed.
        public double ObservedScorePercentage { get; set; }

        public bool IsMine { get; set; }
    }
}
