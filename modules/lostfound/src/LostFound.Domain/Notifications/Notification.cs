using System;
using Volo.Abp.Domain.Entities.Auditing;

namespace LostFound.Notifications
{
    public class Notification : FullAuditedAggregateRoot<Guid>
    {
        // Reporter-keyed (existing match/claim notifications) and
        // IdentityUser-keyed (new: missed calls, and anything else tied to
        // an account rather than a lost/found Reporter) are both first-class
        // now - exactly one of the two is set on any given row.
        // IdentityUserId is a plain column (no cross-module FK), same
        // convention as Reporter.IdentityUserId.
        public virtual Guid? ReporterId { get; private set; }

        public virtual Guid? IdentityUserId { get; private set; }

        public virtual Guid ReportId { get; private set; }

        public virtual string? Title { get; private set; }

        public virtual string? Message { get; private set; }

        public virtual bool IsRead { get; private set; }

        protected Notification()
        {
        }

        public Notification(Guid id, Guid reporterId, Guid reportId, string? title, string? message) : base(id)
        {
            ReporterId = reporterId;
            ReportId = reportId;
            Title = title;
            Message = message;
            IsRead = false;
        }

        private Notification(Guid id, Guid reportId, string? title, string? message, Guid identityUserId) : base(id)
        {
            IdentityUserId = identityUserId;
            ReportId = reportId;
            Title = title;
            Message = message;
            IsRead = false;
        }

        // For account-level notifications (e.g. a missed call) that aren't
        // tied to being a lost/found Reporter at all - see
        // ConversationAppService's missed-call handling. id is deliberately
        // the caller-supplied Agora CallId, not a fresh GuidGenerator value
        // - see that call site for why.
        public static Notification ForIdentityUser(Guid id, Guid identityUserId, Guid reportId, string? title, string? message) =>
            new(id, reportId, title, message, identityUserId);

        public Notification MarkAsRead()
        {
            IsRead = true;
            return this;
        }
    }
}
