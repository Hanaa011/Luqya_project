using System;
using System.Collections.Concurrent;
using Volo.Abp.DependencyInjection;

namespace LostFound.Calls
{
    public enum CallState
    {
        Ringing,
        Connected,
    }

    public class ActiveCall
    {
        public Guid CallId { get; init; }
        public Guid CallerId { get; init; }
        public CallState State { get; set; }
        public DateTime StartedAtUtc { get; init; }
    }

    // Ephemeral, in-memory only - deliberately NOT persisted to the
    // database (Task E: "first try a stateless call-token design"). A
    // conversation's active call is transient signaling state, not
    // something that needs to survive a backend restart; the actual call
    // media lives entirely in Agora's own infrastructure once joined.
    // ISingletonDependency is ABP's own conventional-registration marker -
    // this class is auto-registered as a singleton with zero module
    // wiring needed.
    public class InMemoryCallStateStore : ISingletonDependency
    {
        // An unanswered ring older than this is treated as abandoned
        // (e.g. the caller navigated away without ending the call) and is
        // cleared on next read, rather than ringing forever until the
        // backend restarts.
        private static readonly TimeSpan RingTimeout = TimeSpan.FromSeconds(45);

        private readonly ConcurrentDictionary<Guid, ActiveCall> _calls = new();

        public ActiveCall? Get(Guid conversationId) => Get(conversationId, out _);

        // justMissed is set exactly once per call (TryRemove only succeeds
        // for the single caller that wins the race, so two near-
        // simultaneous polls can never both observe a miss for the same
        // CallId) - the caller uses it to fire the missed-call
        // notification/email exactly once. See
        // ConversationAppService.MapActiveCallToDto.
        public ActiveCall? Get(Guid conversationId, out ActiveCall? justMissed)
        {
            justMissed = null;

            if (!_calls.TryGetValue(conversationId, out var call))
            {
                return null;
            }

            if (call.State == CallState.Ringing && DateTime.UtcNow - call.StartedAtUtc > RingTimeout)
            {
                if (_calls.TryRemove(conversationId, out var removed))
                {
                    justMissed = removed;
                }

                return null;
            }

            return call;
        }

        // Idempotent: returns the existing call if one is already
        // ringing/connected for this conversation (repeated clicks, a
        // page refresh) instead of starting a second concurrent one.
        public ActiveCall StartOrGetExisting(Guid conversationId, Guid callerId)
        {
            return _calls.GetOrAdd(conversationId, _ => new ActiveCall
            {
                CallId = Guid.NewGuid(),
                CallerId = callerId,
                State = CallState.Ringing,
                StartedAtUtc = DateTime.UtcNow,
            });
        }

        public void MarkConnected(Guid conversationId)
        {
            if (_calls.TryGetValue(conversationId, out var call))
            {
                call.State = CallState.Connected;
            }
        }

        public void End(Guid conversationId)
        {
            _calls.TryRemove(conversationId, out _);
        }
    }
}
