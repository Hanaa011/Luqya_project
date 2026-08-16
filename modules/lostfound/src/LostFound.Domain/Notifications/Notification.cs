using System;
using Volo.Abp.Domain.Entities.Auditing;

namespace LostFound.Notifications
{
    public class Notification : FullAuditedAggregateRoot<Guid>
    {
        public virtual Guid ReporterId { get; private set; }

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

        public Notification MarkAsRead()
        {
            IsRead = true;
            return this;
        }
    }
}
