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

        // True when this exact (report, caller) pair already had a
        // "this is my item" claim before this call - the guest contact-
        // request email is never resent for a repeat click; the frontend
        // uses this to show "a contact request was already sent" instead
        // of implying a brand-new one just went out.
        public bool AlreadyRequested { get; set; }
    }
}
