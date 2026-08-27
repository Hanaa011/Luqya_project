import { useCallback, useEffect, useMemo, useState } from "react";

import { useAuth } from "./useAuth";
import { listConversations } from "../api/conversations";
import { ConversationsContext } from "./conversationsContextDef";

// A single background poll of the user's conversation list, shared across
// the whole app (mounted once, above <App/>) - the same source
// IConversationAppService.GetListAsync already serves Messages.jsx, now
// also reused for: the navbar unread badge, per-conversation unread
// indicators, and a cross-page "incoming call" notification (Task 6). No
// new endpoint, no SignalR/WebSockets - just one more consumer of the
// existing polling pattern already used inside Conversation.jsx.
const POLL_INTERVAL_MS = 5000;

export function ConversationsProvider({ children }) {
  const { userId } = useAuth();
  const [conversations, setConversations] = useState([]);
  const [dismissedCallIds, setDismissedCallIds] = useState(() => new Set());
  const [loaded, setLoaded] = useState(false);
  const [loadError, setLoadError] = useState(null);

  const dismissIncomingCall = useCallback((callId) => {
    setDismissedCallIds((prev) => new Set(prev).add(callId));
  }, []);

  const load = useCallback(() => {
    if (!userId) return Promise.resolve();
    return listConversations()
      .then((res) => {
        setConversations(res ?? []);
        setLoadError(null);
      })
      .catch((err) => setLoadError(err.message || ""))
      .finally(() => setLoaded(true));
  }, [userId]);

  useEffect(() => {
    if (!userId) return;

    let cancelled = false;
    const tick = () => {
      if (!cancelled) load();
    };

    tick();
    const intervalId = window.setInterval(tick, POLL_INTERVAL_MS);

    return () => {
      cancelled = true;
      window.clearInterval(intervalId);
    };
  }, [userId, load]);

  const totalUnread = useMemo(
    () => conversations.reduce((sum, c) => sum + (c.unreadCount || 0), 0),
    [conversations]
  );

  // The first conversation with a call ringing FOR me (someone else
  // started it, I haven't joined or declined it yet) - a real second
  // simultaneous incoming call is not a case this app needs to juggle.
  const incomingCall = useMemo(() => {
    const match = conversations.find(
      (c) =>
        c.activeCall &&
        c.activeCall.callerId !== userId &&
        c.activeCall.state === "Ringing" &&
        !dismissedCallIds.has(c.activeCall.callId)
    );

    if (!match) return null;

    return {
      conversationId: match.id,
      callId: match.activeCall.callId,
      callerName: match.otherParticipantName,
    };
  }, [conversations, userId, dismissedCallIds]);

  // Degrade to empty/default whenever there's no signed-in user, rather
  // than resetting the underlying state via an effect (e.g. after logout,
  // or before a different user's first poll has landed) - avoids a stale
  // previous user's data ever being exposed, without a synchronous
  // setState-in-effect reset.
  const value = useMemo(
    () =>
      userId
        ? { conversations, totalUnread, incomingCall, dismissIncomingCall, refresh: load, loaded, loadError }
        : { conversations: [], totalUnread: 0, incomingCall: null, dismissIncomingCall, refresh: load, loaded: false, loadError: null },
    [userId, conversations, totalUnread, incomingCall, dismissIncomingCall, load, loaded, loadError]
  );

  return <ConversationsContext.Provider value={value}>{children}</ConversationsContext.Provider>;
}
