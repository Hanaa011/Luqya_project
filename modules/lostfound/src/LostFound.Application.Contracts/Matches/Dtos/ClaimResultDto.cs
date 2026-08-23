namespace LostFound.Matches.Dtos
{
    // Phase 4 Part 6 (Task B): ClaimAsync's response. When the caller
    // owned an eligible report, this wraps the same, full, two-sided
    // Match ClaimAsync always produced (Match is non-null - exactly the
    // Phase 4 Part 3/5 shape, unchanged). When the caller owned no
    // eligible report, there is no second report to pair into a real
    // Match - Match is null, but ContactAccessGranted can still be true,
    // via the narrower ReportClaim record instead (see MatchManager's
    // GetOrCreateClaimWithoutOwnReportAsync).
    public class ClaimResultDto
    {
        public MatchDto? Match { get; set; }

        public bool ContactAccessGranted { get; set; }
    }
}
