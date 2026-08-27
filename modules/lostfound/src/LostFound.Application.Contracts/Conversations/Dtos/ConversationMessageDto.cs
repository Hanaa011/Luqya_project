using System;

namespace LostFound.Conversations.Dtos
{
    public class ConversationMessageDto
    {
        public Guid Id { get; set; }

        public Guid SenderId { get; set; }

        public string Text { get; set; } = string.Empty;

        public DateTime CreationTime { get; set; }

        public bool IsRead { get; set; }

        // Resolved server-side against the caller's own id, so the
        // frontend never has to compare raw user ids itself for bubble
        // alignment.
        public bool IsMine { get; set; }
    }
}
