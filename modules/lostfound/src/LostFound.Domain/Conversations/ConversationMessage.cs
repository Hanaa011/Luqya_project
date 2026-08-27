using System;
using Volo.Abp.Domain.Entities;

namespace LostFound.Conversations
{
    public class ConversationMessage : Entity<Guid>
    {
        public virtual Guid ConversationId { get; private set; }

        // The sending user's identity user id (CurrentUser.Id) - a plain
        // Guid, no cross-module FK to Identity, matching Report.CreatorId/
        // ReportClaim.ClaimantUserId's own established convention.
        public virtual Guid SenderId { get; private set; }

        public virtual string Text { get; private set; } = string.Empty;

        public virtual DateTime CreationTime { get; private set; }

        public virtual bool IsRead { get; private set; }

        protected ConversationMessage()
        {
        }

        public ConversationMessage(Guid id, Guid conversationId, Guid senderId, string text) : base(id)
        {
            ConversationId = conversationId;
            SenderId = senderId;
            Text = text;
            CreationTime = DateTime.UtcNow;
        }

        public void MarkAsRead() => IsRead = true;
    }
}
