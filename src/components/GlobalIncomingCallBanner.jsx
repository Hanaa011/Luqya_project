import { useState } from "react";
import { useLocation, useNavigate } from "react-router-dom";
import { Phone, PhoneOff, PhoneIncoming } from "lucide-react";

import { useI18n } from "../lib/useI18n";
import { useConversations } from "../lib/useConversations";
import { endCall } from "../api/calls";

function copy(lang, values) {
  return values[lang] ?? values.en;
}

// Task 6: an incoming call must be visible from ANY page, not just the
// conversation itself - Conversation.jsx already renders its own incoming
// banner when you're on that exact conversation's page, so this is
// suppressed there to avoid showing the same call twice.
export default function GlobalIncomingCallBanner() {
  const { lang } = useI18n();
  const { incomingCall, dismissIncomingCall } = useConversations();
  const location = useLocation();
  const navigate = useNavigate();
  const [declining, setDeclining] = useState(false);

  if (!incomingCall) return null;
  if (location.pathname === `/messages/${incomingCall.conversationId}`) return null;

  async function handleDecline() {
    setDeclining(true);
    dismissIncomingCall(incomingCall.callId);
    try {
      await endCall(incomingCall.conversationId);
    } catch {
      // Non-fatal - already dismissed locally either way.
    } finally {
      setDeclining(false);
    }
  }

  function handleAccept() {
    navigate(`/messages/${incomingCall.conversationId}`, { state: { autoAcceptCallId: incomingCall.callId } });
  }

  return (
    <div className="fixed inset-x-0 top-16 sm:top-20 z-[60] flex justify-center px-4 pointer-events-none">
      <div className="pointer-events-auto flex items-center gap-3 rounded-2xl border border-primary/25 bg-card/95 backdrop-blur-xl px-5 py-3 shadow-luxe animate-rise-in">
        <span className="grid size-10 shrink-0 place-items-center rounded-full bg-primary/10 text-primary">
          <PhoneIncoming className="size-4 animate-pulse" />
        </span>
        <div className="min-w-0">
          <p className="text-sm font-bold truncate">
            {copy(lang, {
              ar: `مكالمة واردة من ${incomingCall.callerName}`,
              en: `Incoming voice call from ${incomingCall.callerName}`,
              ur: `${incomingCall.callerName} کی جانب سے صوتی کال`,
            })}
          </p>
        </div>
        <div className="flex items-center gap-2 shrink-0">
          <button
            type="button"
            onClick={handleDecline}
            disabled={declining}
            title={copy(lang, { ar: "رفض", en: "Decline", ur: "مسترد کریں" })}
            className="size-9 rounded-full bg-error-tint text-error grid place-items-center hover:opacity-80 transition-opacity disabled:opacity-50"
          >
            <PhoneOff className="size-4" />
          </button>
          <button
            type="button"
            onClick={handleAccept}
            title={copy(lang, { ar: "قبول", en: "Accept", ur: "قبول کریں" })}
            className="size-9 rounded-full bg-success-tint text-success grid place-items-center hover:opacity-80 transition-opacity"
          >
            <Phone className="size-4" />
          </button>
        </div>
      </div>
    </div>
  );
}
