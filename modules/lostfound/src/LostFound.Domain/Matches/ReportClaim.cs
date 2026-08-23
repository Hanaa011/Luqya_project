using System;
using Volo.Abp.Domain.Entities.Auditing;

namespace LostFound.Matches
{
    // Phase 4 Part 6 (Task B): a minimal, narrowly-scoped record of "this
    // authenticated user recorded a disposition toward this specific
    // report, at this specific time" - used whenever the mechanics don't
    // require (or, per Phase 4 Part 8, never require) a real, two-sided
    // Match (Match.cs requires both a LostReportId and a FoundReportId).
    // This is deliberately NOT a variant of Match: it has no opposite-side
    // report and does not appear in any "both parties' Dashboard" view.
    //
    // Phase 4 Part 8 (Task B): extended to also represent "is NOT mine",
    // via IsMine - the same entity/table, not a second parallel mechanism,
    // per this task's explicit instruction to reuse the established
    // pattern. IsMine=true keeps Part 6's exact original behavior
    // (immediate contact access - see ReporterAppService's
    // GetRelatedReporterIdsQueryableAsync, filtered to IsMine=true only -
    // and a one-sided Notification to the report's own reporter).
    // IsMine=false grants no contact access at all and sends no
    // notification (a silent dismissal was Phase 4 Part 3's original,
    // deliberate design for "not my item" - see MatchAppService.ClaimAsync's
    // own history - and this preserves that, just without requiring an
    // own report to record it against anymore).
    public class ReportClaim : FullAuditedAggregateRoot<Guid>
    {
        public virtual Guid ReportId { get; private set; }

        // The claiming user's identity user id (CurrentUser.Id) - not a
        // ReporterId. Mirrors Report.CreatorId's own convention (a plain
        // Guid, no cross-module FK to the Identity module) used throughout
        // this module's own ownership checks (e.g. ReporterAppService's
        // `r.CreatorId == CurrentUser.Id`).
        public virtual Guid ClaimantUserId { get; private set; }

        // The exact score the claimant was shown at claim time, for the
        // same "stored verbatim, never recomputed" reason as
        // ClaimMatchDto.ObservedScorePercentage - an audit trail for this
        // narrower access grant, not a similarity score used for ranking.
        public virtual decimal? ObservedScorePercentage { get; private set; }

        // true = "this is my item" (grants contact access); false = "not
        // my item" (a recorded dismissal only - never grants contact
        // access, never notifies anyone). One row per (ReportId,
        // ClaimantUserId) - a user's disposition toward a given report is
        // singular and can change (see MatchManager.GetOrCreateReportClaimAsync's
        // update-in-place handling), not accumulated as separate rows.
        public virtual bool IsMine { get; private set; }

        protected ReportClaim()
        {
        }

        public ReportClaim(Guid id, Guid reportId, Guid claimantUserId, bool isMine, decimal? observedScorePercentage) : base(id)
        {
            ReportId = reportId;
            ClaimantUserId = claimantUserId;
            IsMine = isMine;
            ObservedScorePercentage = observedScorePercentage;
        }

        public ReportClaim UpdateDisposition(bool isMine, decimal? observedScorePercentage)
        {
            IsMine = isMine;
            ObservedScorePercentage = observedScorePercentage;
            return this;
        }
    }
}
