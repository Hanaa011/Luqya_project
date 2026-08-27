using System;
using System.Collections.Generic;
using Volo.Abp.Domain.Entities.Auditing;

namespace LostFound.Conversations
{
    // Private, in-platform messaging between two users about one report -
    // replaces direct phone/email exposure (see IConversationAppService).
    // Participant order is normalized at creation (smaller Guid first) so
    // the same two users about the same report always resolve to the same
    // row, regardless of who started it - see
    // ConversationAppService.GetOrCreateForReportAsync.
    public class Conversation : FullAuditedAggregateRoot<Guid>
    {
        public virtual Guid ReportId { get; private set; }

        public virtual Guid Participant1Id { get; private set; }

        public virtual Guid Participant2Id { get; private set; }

        // Messages are a child collection of this aggregate, not a separate
        // aggregate root - they have no independent lifecycle or access
        // pattern, so there is deliberately no IConversationMessageRepository.
        public virtual ICollection<ConversationMessage> Messages { get; private set; } = new List<ConversationMessage>();

        protected Conversation()
        {
        }

        public Conversation(Guid id, Guid reportId, Guid participant1Id, Guid participant2Id) : base(id)
        {
            ReportId = reportId;
            Participant1Id = participant1Id;
            Participant2Id = participant2Id;
        }

        public bool HasParticipant(Guid userId) => Participant1Id == userId || Participant2Id == userId;

        public ConversationMessage AddMessage(Guid id, Guid senderId, string text)
        {
            var message = new ConversationMessage(id, Id, senderId, text);
            Messages.Add(message);
            return message;
        }

        // Called when readerId opens/polls this conversation - only the
        // OTHER participant's unread messages are affected, never the
        // reader's own sent messages (a sender's own message is never
        // "unread" from their perspective).
        public bool MarkMessagesReadFor(Guid readerId)
        {
            var changed = false;

            foreach (var message in Messages)
            {
                if (message.SenderId != readerId && !message.IsRead)
                {
                    message.MarkAsRead();
                    changed = true;
                }
            }

            return changed;
        }
    }
}
