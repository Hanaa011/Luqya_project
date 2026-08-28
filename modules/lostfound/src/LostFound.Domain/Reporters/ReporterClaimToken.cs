using System;
using Volo.Abp.Domain.Entities.Auditing;

namespace LostFound.Reporters
{
    // A single-use, time-limited proof that whoever redeems it controls the
    // guest Reporter's on-file email - the mechanism behind "This is my
    // item" on a guest report: the finder's click triggers one of these,
    // emailed to the guest, and redeeming it (after logging in/registering)
    // links Reporter.IdentityUserId. Only the hash is ever persisted - the
    // raw token exists only in memory and in the outgoing email.
    public class ReporterClaimToken : FullAuditedAggregateRoot<Guid>
    {
        public virtual Guid ReporterId { get; private set; }

        public virtual string TokenHash { get; private set; }

        public virtual DateTime ExpiresAt { get; private set; }

        public virtual DateTime? UsedAt { get; private set; }

        protected ReporterClaimToken()
        {
            TokenHash = string.Empty;
        }

        internal ReporterClaimToken(Guid id, Guid reporterId, string tokenHash, DateTime expiresAt) : base(id)
        {
            ReporterId = reporterId;
            TokenHash = tokenHash;
            ExpiresAt = expiresAt;
        }

        internal bool IsValid(DateTime utcNow) => UsedAt == null && ExpiresAt > utcNow;

        internal void MarkUsed(DateTime utcNow) => UsedAt = utcNow;
    }
}
