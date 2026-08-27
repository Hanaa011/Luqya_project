using System;
using System.Collections.Generic;
using LostFound.Reports;

namespace LostFound.Conversations.Dtos
{
    public class ConversationDto
    {
        public Guid Id { get; set; }

        public Guid ReportId { get; set; }

        public DateTime CreationTime { get; set; }

        // Safe identity info only, resolved server-side from the OTHER
        // participant's IdentityUser (name/surname/username) - never
        // phone/email. See ConversationAppService.ResolveDisplayName.
        public string OtherParticipantName { get; set; } = string.Empty;

        // Denormalized report context so a conversation is always
        // recognizable without a second fetch (Task: "must clearly show
        // which report/item it belongs to").
        public string? ReportDescription { get; set; }

        public ReportType ReportType { get; set; }

        // True once the report is ReportStatus.Closed - new messages are
        // blocked (see ConversationAppService.SendMessageAsync) but
        // existing history is always still returned.
        public bool ReportIsClosed { get; set; }

        // Full history for GetAsync; a single latest-message entry for
        // GetListAsync's preview; null if the conversation has no
        // messages yet.
        public List<ConversationMessageDto>? Messages { get; set; }

        // Messages from the OTHER participant not yet marked read, as of
        // this fetch. GetAsync marks them read (see MarkMessagesReadFor)
        // before mapping, so this is always 0 right after opening a
        // conversation; GetListAsync never marks anything read, so this
        // reflects the true unread count for the navbar/list badges.
        public int UnreadCount { get; set; }

        // Phase 2 (voice calling): null when no call is ringing/connected.
        // Populated from the in-memory call state store on every fetch, so
        // the existing polling loop is the entire incoming-call signaling
        // mechanism - no separate endpoint or realtime channel.
        public ActiveCallDto? ActiveCall { get; set; }
    }
}
