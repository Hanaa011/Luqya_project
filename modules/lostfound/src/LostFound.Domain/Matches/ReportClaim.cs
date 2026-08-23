using System;
using Volo.Abp.Domain.Entities.Auditing;

namespace LostFound.Matches
{
    // Phase 4 Part 6 (Task B): a minimal, narrowly-scoped record of "this
    // authenticated user confirmed 'this is my item' for this specific
    // report, at this specific time" - used only when the claiming user
    // has no eligible own report to pair into a full, two-sided Match
    // (Match.cs requires both a LostReportId and a FoundReportId; there is
    // no second report to supply in this case). This is deliberately NOT a
    // variant of Match: it has no opposite-side report, does not appear in
    // any "both parties' Dashboard" view, and does not trigger the
    // both-reporters Notification a real Match creates. Its only effect is
    // granting the claimant contact access to this one report's reporter -
    // see ReporterAppService.GetRelatedReporterIdsQueryableAsync, which
    // this record extends, not replaces or weakens.
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

        protected ReportClaim()
        {
        }

        public ReportClaim(Guid id, Guid reportId, Guid claimantUserId, decimal? observedScorePercentage) : base(id)
        {
            ReportId = reportId;
            ClaimantUserId = claimantUserId;
            ObservedScorePercentage = observedScorePercentage;
        }
    }
}
