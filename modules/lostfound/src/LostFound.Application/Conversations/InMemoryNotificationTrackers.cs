using System;
using System.Collections.Concurrent;
using Volo.Abp.DependencyInjection;

namespace LostFound.Conversations
{
    // Ephemeral, in-memory only - same rationale and pattern as Calls
    // /InMemoryCallStateStore: this is transient "who's around right now"
    // signaling, not something that needs to survive a backend restart.
    // Tracks the last moment each (conversationId, userId) pair was seen
    // actively viewing that conversation (ConversationAppService.GetAsync
    // - the poll endpoint - marks this on every call). A new message
    // skips the email notification for whichever participant's mark is
    // still fresh.
    public class InMemoryConversationPresenceTracker : ISingletonDependency
    {
        // A little more than Conversation.jsx's own 3s poll interval, so
        // one missed beat (a slow request, a brief tab switch) doesn't
        // falsely read as "not viewing" and fire an unnecessary email.
        private static readonly TimeSpan ActiveWindow = TimeSpan.FromSeconds(10);

        private readonly ConcurrentDictionary<(Guid ConversationId, Guid UserId), DateTime> _lastSeenUtc = new();

        public void MarkActive(Guid conversationId, Guid userId) =>
            _lastSeenUtc[(conversationId, userId)] = DateTime.UtcNow;

        public bool IsActive(Guid conversationId, Guid userId) =>
            _lastSeenUtc.TryGetValue((conversationId, userId), out var lastSeen) &&
            DateTime.UtcNow - lastSeen <= ActiveWindow;
    }

    // Leading-edge throttle for the "new message" email: the first
    // message after the cooldown expires sends one email immediately and
    // starts a fresh cooldown; every other message in that window is
    // silently skipped (no queued/delayed "digest" email - see
    // ConversationAppService.SendMessageAsync). Ephemeral for the same
    // reason as the presence tracker above - losing a cooldown window on
    // a rare backend restart just means one extra email gets through, not
    // a correctness problem.
    public class InMemoryMessageEmailCooldownTracker : ISingletonDependency
    {
        private static readonly TimeSpan CooldownWindow = TimeSpan.FromMinutes(5);

        private readonly ConcurrentDictionary<(Guid ConversationId, Guid RecipientId), DateTime> _lastSentUtc = new();

        // Atomically checks-and-marks: returns true (send it) only for the
        // caller that actually claims the cooldown window, so two
        // near-simultaneous messages can never both slip through.
        public bool ShouldSend(Guid conversationId, Guid recipientId)
        {
            var key = (conversationId, recipientId);
            var now = DateTime.UtcNow;

            var updated = _lastSentUtc.AddOrUpdate(
                key,
                now,
                (_, existing) => now - existing > CooldownWindow ? now : existing);

            return updated == now;
        }
    }
}
