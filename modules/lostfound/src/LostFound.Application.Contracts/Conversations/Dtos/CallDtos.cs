using System;

namespace LostFound.Conversations.Dtos
{
    // Rides on the existing conversation poll (ConversationDto.ActiveCall)
    // rather than a new poll loop - this is the entire incoming-call
    // signaling mechanism, no SignalR/WebSockets involved.
    public class ActiveCallDto
    {
        public Guid CallId { get; set; }

        public Guid CallerId { get; set; }

        // "Ringing" | "Connected" - matches CallState's ToString().
        public string State { get; set; } = string.Empty;

        public DateTime StartedAtUtc { get; set; }
    }

    // Never carries the Agora App Certificate - only what the Agora Web
    // SDK needs client-side to join one specific channel.
    public class CallCredentialsDto
    {
        public Guid CallId { get; set; }

        public string AppId { get; set; } = string.Empty;

        public string ChannelName { get; set; } = string.Empty;

        public string Token { get; set; } = string.Empty;

        public uint Uid { get; set; }

        public DateTime ExpiresAtUtc { get; set; }
    }
}
