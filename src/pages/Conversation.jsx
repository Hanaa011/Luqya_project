import { useEffect, useRef, useState } from "react";
import { Link, useLocation, useNavigate, useParams } from "react-router-dom";
import {
  ArrowRight,
  Loader2,
  AlertCircle,
  Send,
  Phone,
  PhoneOff,
  PhoneIncoming,
  Mic,
  MicOff,
  PackageCheck,
  Check,
  CheckCheck,
} from "lucide-react";

import { useI18n } from "../lib/useI18n";
import { useAuth } from "../lib/useAuth";
import { getConversation, sendConversationMessage } from "../api/conversations";
import { startCall, joinCall, endCall } from "../api/calls";
import { useAgoraCall } from "../hooks/useAgoraCall";
import { ReportType } from "../api/enums";
import { ApiError } from "../api/httpClient";

function copy(lang, values) {
  return values[lang] ?? values.en;
}

function formatTime(iso) {
  if (!iso) return "";
  try {
    return new Date(iso).toLocaleTimeString(undefined, { hour: "2-digit", minute: "2-digit" });
  } catch {
    return "";
  }
}

// 00:32 / 12:05 style duration, per the call-UX polish requirement.
function formatDuration(totalSeconds) {
  const m = Math.floor(totalSeconds / 60).toString().padStart(2, "0");
  const s = Math.floor(totalSeconds % 60).toString().padStart(2, "0");
  return `${m}:${s}`;
}

// No SignalR/real-time infrastructure exists in this backend - simple
// polling while the page is open is the smallest reliable way to
// approximate live updates without adding a dependency. The same poll
// also carries call state (ConversationDto.ActiveCall), which is the
// entire incoming-call signaling mechanism - no second poll loop.
const POLL_INTERVAL_MS = 3000;

export default function Conversation() {
  const { id } = useParams();
  const { tr, lang } = useI18n();
  const { userId } = useAuth();
  const location = useLocation();
  const navigate = useNavigate();
  // Set by GlobalIncomingCallBanner's Accept button when it navigated here
  // from another page - fires handleAcceptCall once, then never again for
  // this mount (a poll refresh must not re-trigger it).
  const autoAcceptedRef = useRef(false);
  const [status, setStatus] = useState("loading"); // loading | ready | error | forbidden
  const [conversation, setConversation] = useState(null);
  const [errorMsg, setErrorMsg] = useState(null);
  const [text, setText] = useState("");
  const [sending, setSending] = useState(false);
  const bottomRef = useRef(null);

  const agoraCall = useAgoraCall();
  const [callActionPending, setCallActionPending] = useState(false);
  // The call id my local Agora session currently belongs to, if any -
  // distinguishes "a call is ringing that I've already joined" from "a
  // fresh incoming call I haven't responded to yet".
  const myCallIdRef = useRef(null);
  // A call id I've explicitly declined/ended - keeps its incoming banner
  // from reappearing on the next poll tick before the backend clears it.
  // Real state (not a ref) since declining must re-render to hide the banner.
  const [dismissedCallId, setDismissedCallId] = useState(null);
  // Call-UX polish: a friendly, transient explanation for how the last call
  // ended (declined/unanswered/ended/missed) - the backend's
  // InMemoryCallStateStore doesn't distinguish "declined" from "unanswered"
  // (both just remove the call), so this reads the last known CallState
  // (Ringing vs Connected) to give an honest, not-fabricated distinction.
  const [callNotice, setCallNotice] = useState(null);
  const lastActiveCallRef = useRef(null);
  const [elapsedSeconds, setElapsedSeconds] = useState(0);
  const callStartRef = useRef(null);

  useEffect(() => {
    document.title = tr({ ar: "محادثة — لُقيا", en: "Conversation — Luqya", ur: "بات چیت — لقیا" });
  }, [tr]);

  useEffect(() => {
    let cancelled = false;

    function load(isInitial) {
      getConversation(id)
        .then((res) => {
          if (cancelled) return;
          setConversation(res);
          setStatus("ready");
        })
        .catch((err) => {
          if (cancelled || !isInitial) return;
          // Backend verifies participation server-side (Task D) - a 403
          // here means this user genuinely isn't part of the conversation,
          // not just a UI-state guess.
          setStatus(err instanceof ApiError && err.isForbidden ? "forbidden" : "error");
          setErrorMsg(err.message || "");
        });
    }

    load(true);
    const intervalId = window.setInterval(() => load(false), POLL_INTERVAL_MS);

    return () => {
      cancelled = true;
      window.clearInterval(intervalId);
    };
  }, [id]);

  // Auto-accept an incoming call when arriving here via the global
  // cross-page notification's Accept button (Task 6) - the call itself is
  // still joined through the normal handleAcceptCall flow below, this just
  // triggers it automatically instead of making the user click Accept
  // again on a banner they already accepted.
  useEffect(() => {
    const autoId = location.state?.autoAcceptCallId;
    if (!autoId || autoAcceptedRef.current) return;
    const call = conversation?.activeCall;
    if (call?.callId === autoId && call.callerId !== userId && agoraCall.phase === "idle") {
      autoAcceptedRef.current = true;
      navigate(location.pathname, { replace: true, state: {} });
      handleAcceptCall();
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [conversation?.activeCall, location.state]);

  // If the call I'm locally in (or was offered) disappears from the next
  // poll - the other participant declined, ended it, or the 45s ring
  // timeout elapsed - clean up my own Agora session too, instead of
  // leaving it dangling, and surface a friendly explanation of what
  // happened (see callNotice above).
  useEffect(() => {
    const current = conversation?.activeCall ?? null;

    if (current) {
      lastActiveCallRef.current = current;
      return;
    }

    const previous = lastActiveCallRef.current;
    lastActiveCallRef.current = null;
    if (!previous) return;

    if (myCallIdRef.current && agoraCall.phase !== "idle") {
      myCallIdRef.current = null;
      agoraCall.leaveChannel();
      setCallNotice(
        previous.state === "Connected"
          ? copy(lang, { ar: "انتهت المكالمة.", en: "Call ended.", ur: "کال ختم ہو گئی۔" })
          : copy(lang, {
              ar: "لم يتم الرد على المكالمة.",
              en: "The call wasn't answered.",
              ur: "کال کا جواب نہیں دیا گیا۔",
            })
      );
    } else if (previous.callerId !== userId && previous.callId !== dismissedCallId) {
      // An incoming call I never joined disappeared on its own (the
      // caller hung up, or it rang out) - not something I declined.
      setCallNotice(
        copy(lang, {
          ar: `مكالمة فائتة من ${conversation?.otherParticipantName ?? ""}`,
          en: `Missed call from ${conversation?.otherParticipantName ?? ""}`,
          ur: `${conversation?.otherParticipantName ?? ""} کی چھوٹی ہوئی کال`,
        })
      );
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [conversation?.activeCall]);

  // Auto-clear the transient call notice so it doesn't linger indefinitely.
  useEffect(() => {
    if (!callNotice) return;
    const timeoutId = window.setTimeout(() => setCallNotice(null), 6000);
    return () => window.clearTimeout(timeoutId);
  }, [callNotice]);

  // Best-effort: also end the call server-side on navigation/unmount, not
  // just on an explicit End Call click, so the other participant's next
  // poll doesn't keep showing a call that's already gone on this side.
  useEffect(() => {
    return () => {
      if (myCallIdRef.current) {
        endCall(id).catch(() => {});
      }
    };
  }, [id]);

  useEffect(() => {
    bottomRef.current?.scrollIntoView({ block: "end" });
  }, [conversation?.messages?.length]);

  // Call duration timer (00:32 format). "Truly connected" excludes the
  // caller's own outgoing-ringing window - my local Agora session is
  // already "connected" the moment I join and publish, before the callee
  // has answered (see isRingingOutgoing below), so the timer must wait for
  // the backend's CallState to actually flip to Connected too.
  const activeCallState = conversation?.activeCall?.state ?? null;
  const isCaller = conversation?.activeCall?.callerId === userId;
  const isTrulyConnected =
    agoraCall.phase === "connected" && !(isCaller && activeCallState === "Ringing");

  useEffect(() => {
    if (!isTrulyConnected) {
      callStartRef.current = null;
      return;
    }

    if (!callStartRef.current) callStartRef.current = Date.now();
    const intervalId = window.setInterval(() => {
      setElapsedSeconds(Math.floor((Date.now() - callStartRef.current) / 1000));
    }, 1000);

    return () => window.clearInterval(intervalId);
  }, [isTrulyConnected]);

  async function handleSend(event) {
    event.preventDefault();
    const trimmed = text.trim();
    if (!trimmed || sending) return;

    setSending(true);
    try {
      await sendConversationMessage(id, trimmed);
      setText("");
      const fresh = await getConversation(id);
      setConversation(fresh);
    } catch (err) {
      setErrorMsg(err.message || "");
    } finally {
      setSending(false);
    }
  }

  async function handleStartCall() {
    if (callActionPending || agoraCall.phase !== "idle" || conversation?.activeCall) return;

    setCallActionPending(true);
    setCallNotice(null);
    try {
      const credentials = await startCall(id);
      myCallIdRef.current = credentials.callId;
      await agoraCall.joinChannel(credentials);
    } catch (err) {
      setErrorMsg(err.message || "");
    } finally {
      setCallActionPending(false);
    }
  }

  async function handleAcceptCall() {
    if (callActionPending || agoraCall.phase !== "idle") return;

    setCallActionPending(true);
    setCallNotice(null);
    try {
      const credentials = await joinCall(id);
      myCallIdRef.current = credentials.callId;
      await agoraCall.joinChannel(credentials);
    } catch (err) {
      setErrorMsg(err.message || "");
    } finally {
      setCallActionPending(false);
    }
  }

  async function handleDeclineCall() {
    setDismissedCallId(conversation?.activeCall?.callId ?? null);
    try {
      await endCall(id);
    } catch {
      // Non-fatal - the next poll will still show it if this genuinely
      // failed, and the user can decline again.
    }
  }

  async function handleEndCall() {
    myCallIdRef.current = null;
    await agoraCall.leaveChannel();
    try {
      await endCall(id);
    } catch {
      // Non-fatal - my own side is already cleaned up locally either way.
    }
  }

  if (status === "loading") {
    return (
      <section className="py-16 lg:py-24">
        <div className="flex items-center justify-center py-16">
          <Loader2 className="size-6 animate-spin text-muted-foreground" />
        </div>
      </section>
    );
  }

  if (status === "forbidden" || status === "error") {
    return (
      <section className="py-16 lg:py-24">
        <div className="max-w-lg mx-auto px-6 flex flex-col items-center gap-3 py-16 text-center">
          <AlertCircle className="size-6 text-error" />
          <p className="text-error text-sm">
            {status === "forbidden"
              ? copy(lang, {
                  ar: "لا يمكنك الوصول إلى هذه المحادثة.",
                  en: "You don't have access to this conversation.",
                  ur: "آپ کو اس بات چیت تک رسائی نہیں ہے۔",
                })
              : errorMsg}
          </p>
          <Link to="/messages" className="text-sm font-semibold text-primary hover:underline">
            {copy(lang, { ar: "العودة إلى الرسائل", en: "Back to messages", ur: "پیغامات پر واپس جائیں" })}
          </Link>
        </div>
      </section>
    );
  }

  const messages = conversation?.messages ?? [];
  const activeCall = conversation?.activeCall ?? null;
  const isIncomingCall =
    Boolean(activeCall) &&
    activeCall.callerId !== userId &&
    agoraCall.phase === "idle" &&
    activeCall.callId !== dismissedCallId;
  const isInCallUi = agoraCall.phase === "connecting" || agoraCall.phase === "connected" || agoraCall.phase === "error";
  const isRingingOutgoing =
    agoraCall.phase === "connected" && activeCall?.state === "Ringing" && activeCall.callerId === userId;

  const showCallCard = isIncomingCall || isInCallUi;

  return (
    <section className="py-10 lg:py-16">
      <div className="max-w-2xl lg:max-w-5xl mx-auto px-6">
        <Link
          to="/messages"
          className="inline-flex items-center gap-1.5 text-sm font-semibold text-muted-foreground hover:text-primary transition-colors mb-6"
        >
          <ArrowRight className={`size-4 ${lang === "ar" || lang === "ur" ? "" : "rotate-180"}`} />
          {copy(lang, { ar: "الرسائل", en: "Messages", ur: "پیغامات" })}
        </Link>

        <div className="lg:flex lg:items-start lg:gap-5">
          <div className="rounded-[2rem] border border-border bg-card shadow-soft overflow-hidden flex flex-col h-[70vh] lg:flex-1 lg:min-w-0">
            {/* Report context header - "must clearly show which report/item
                it belongs to". No phone/email anywhere on this page. */}
            <div className="px-6 py-4 border-b border-border flex items-center justify-between gap-3 shrink-0">
              <div className="min-w-0">
                <p className="text-sm font-bold truncate">{conversation.otherParticipantName}</p>
                <p className="text-xs text-muted-foreground truncate">
                  {conversation.reportType === ReportType.FOUND
                    ? copy(lang, { ar: "بخصوص غرض موجود", en: "About a found item", ur: "ملنے والی چیز کے بارے میں" })
                    : copy(lang, { ar: "بخصوص غرض مفقود", en: "About a lost item", ur: "کھوئی ہوئی چیز کے بارے میں" })}
                  {conversation.reportDescription ? ` · ${conversation.reportDescription}` : ""}
                </p>
              </div>

              <Link
                to={`/match/${conversation.reportId}`}
                className="text-xs font-semibold text-primary hover:underline shrink-0"
              >
                {copy(lang, { ar: "عرض البلاغ", en: "View report", ur: "رپورٹ دیکھیں" })}
              </Link>

              {/* Phase 2: the same phone button from Phase 1, now wired to a
                  real 1:1 voice call instead of a disabled placeholder. */}
              <button
                type="button"
                onClick={handleStartCall}
                disabled={callActionPending || agoraCall.phase !== "idle" || Boolean(activeCall)}
                title={copy(lang, { ar: "مكالمة صوتية", en: "Voice call", ur: "صوتی کال" })}
                className="size-9 rounded-full border border-border grid place-items-center text-foreground/70 hover:text-primary hover:border-primary/40 transition-colors shrink-0 disabled:opacity-50 disabled:cursor-not-allowed"
              >
                <Phone className="size-4" />
              </button>
            </div>

            {!showCallCard && callNotice && (
              <div className="px-6 py-2.5 bg-stone-50 border-b border-border text-xs font-semibold text-muted-foreground text-center shrink-0">
                {callNotice}
              </div>
            )}

            <div className="flex-1 overflow-y-auto px-6 py-5 space-y-3">
              {messages.length === 0 ? (
                <p className="text-center text-sm text-muted-foreground py-10">
                  {copy(lang, {
                    ar: "لا رسائل بعد. ابدأ المحادثة!",
                    en: "No messages yet. Say hello!",
                    ur: "ابھی کوئی پیغام نہیں۔ بات چیت شروع کریں!",
                  })}
                </p>
              ) : (
                messages.map((m) => (
                  <div key={m.id} className={`flex ${m.isMine ? "justify-end" : "justify-start"}`}>
                    <div
                      className={`max-w-[75%] rounded-2xl px-4 py-2.5 text-sm ${
                        m.isMine ? "bg-primary text-primary-foreground" : "bg-stone-100 text-foreground"
                      }`}
                    >
                      <p className="whitespace-pre-wrap break-words">{m.text}</p>
                      <p
                        className={`flex items-center justify-end gap-1 text-[10px] mt-1 ${
                          m.isMine ? "text-primary-foreground/70" : "text-muted-foreground"
                        }`}
                      >
                        {formatTime(m.creationTime)}
                        {/* Simple sent/read indicator - single check once
                            sent, double check once the recipient has opened
                            the conversation (see ConversationAppService.
                            GetAsync's MarkMessagesReadFor). */}
                        {m.isMine &&
                          (m.isRead ? (
                            <CheckCheck className="size-3" />
                          ) : (
                            <Check className="size-3" />
                          ))}
                      </p>
                    </div>
                  </div>
                ))
              )}
              <div ref={bottomRef} />
            </div>

            <div className="border-t border-border p-4 shrink-0">
            {conversation.reportIsClosed ? (
              <div className="flex items-center gap-2 text-sm text-muted-foreground justify-center py-2">
                <PackageCheck className="size-4" />
                {copy(lang, {
                  ar: "هذا البلاغ مغلق. لا يمكن إرسال رسائل جديدة.",
                  en: "This report is closed. New messages can't be sent.",
                  ur: "یہ رپورٹ بند ہے۔ نئے پیغامات نہیں بھیجے جا سکتے۔",
                })}
              </div>
            ) : (
              <form onSubmit={handleSend} className="flex items-center gap-2">
                <input
                  type="text"
                  value={text}
                  onChange={(e) => setText(e.target.value)}
                  placeholder={copy(lang, { ar: "اكتب رسالة...", en: "Type a message...", ur: "پیغام لکھیں..." })}
                  className="flex-1 px-4 py-3 rounded-2xl bg-stone-50 border border-stone-200 focus:border-primary focus:outline-none focus:ring-2 focus:ring-primary/10 transition-all text-sm"
                />
                <button
                  type="submit"
                  disabled={!text.trim() || sending}
                  className="inline-flex items-center gap-1.5 bg-primary text-primary-foreground px-5 py-3 rounded-2xl font-semibold shadow-glow hover:-translate-y-0.5 transition-transform disabled:opacity-60 disabled:translate-y-0 text-sm shrink-0"
                >
                  {sending ? <Loader2 className="size-4 animate-spin" /> : <Send className="size-4" />}
                  {copy(lang, { ar: "إرسال", en: "Send", ur: "بھیجیں" })}
                </button>
              </form>
            )}
          </div>
          </div>

          {showCallCard && (
            <CallCard
              lang={lang}
              phase={agoraCall.phase}
              isMuted={agoraCall.isMuted}
              errorMessage={agoraCall.errorMessage}
              isIncoming={isIncomingCall}
              isRingingOutgoing={isRingingOutgoing}
              isTrulyConnected={isTrulyConnected}
              elapsedSeconds={elapsedSeconds}
              callActionPending={callActionPending}
              otherParticipantName={conversation.otherParticipantName}
              onAccept={handleAcceptCall}
              onDecline={handleDeclineCall}
              onEnd={handleEndCall}
              onToggleMute={agoraCall.toggleMute}
            />
          )}
        </div>
      </div>
    </section>
  );
}

// A dedicated, professional call card - Task 4: "chat + compact call card
// beside it" on desktop, floating/fixed on mobile so it never disrupts
// message scrolling. Same Agora phases/state as before, just a richer,
// unmistakable presentation: avatar, name, explicit status line, duration
// timer, and a "Muted" text badge (not just an icon swap).
function CallCard({
  lang,
  phase,
  isMuted,
  errorMessage,
  isIncoming,
  isRingingOutgoing,
  isTrulyConnected,
  elapsedSeconds,
  callActionPending,
  otherParticipantName,
  onAccept,
  onDecline,
  onEnd,
  onToggleMute,
}) {
  const tone = phase === "error" ? "error" : isTrulyConnected ? "success" : "primary";
  const toneClasses = {
    error: { ring: "ring-error/25", bg: "bg-error-tint/30", text: "text-error", avatarBg: "bg-error-tint" },
    success: { ring: "ring-success/25", bg: "bg-success-tint/30", text: "text-success", avatarBg: "bg-success-tint" },
    primary: { ring: "ring-primary/25", bg: "bg-primary/[0.06]", text: "text-primary", avatarBg: "bg-primary/10" },
  }[tone];

  const statusText = isIncoming
    ? copy(lang, {
        ar: `مكالمة واردة من ${otherParticipantName}`,
        en: `Incoming call from ${otherParticipantName}`,
        ur: `${otherParticipantName} کی جانب سے کال آ رہی ہے`,
      })
    : phase === "error"
      ? errorMessage || copy(lang, { ar: "تعذّرت المكالمة", en: "Call failed", ur: "کال ناکام ہو گئی" })
      : phase === "connecting"
        ? copy(lang, { ar: "جارٍ الاتصال...", en: "Connecting...", ur: "رابطہ ہو رہا ہے..." })
        : isRingingOutgoing
          ? copy(lang, {
              ar: `جارٍ الاتصال بـ ${otherParticipantName}...`,
              en: `Calling ${otherParticipantName}...`,
              ur: `${otherParticipantName} کو کال کی جا رہی ہے...`,
            })
          : copy(lang, { ar: "متصل", en: "Connected", ur: "منسلک" });

  return (
    <div
      className={`fixed inset-x-4 top-20 z-40 lg:static lg:inset-auto lg:top-auto lg:z-auto lg:w-72 lg:shrink-0 rounded-[1.75rem] border border-border bg-card shadow-luxe ring-1 ${toneClasses.ring} p-5 animate-rise-in`}
    >
      <div className="flex flex-col items-center text-center gap-1">
        <span
          className={`grid size-16 place-items-center rounded-full font-bold font-mono text-lg ${toneClasses.avatarBg} ${toneClasses.text}`}
        >
          {otherParticipantName?.slice(0, 2).toUpperCase() || "?"}
        </span>
        <p className="mt-2 text-sm font-bold truncate max-w-full">{otherParticipantName}</p>
        <p className={`text-xs font-semibold ${toneClasses.text} flex items-center gap-1.5 justify-center`}>
          {phase === "error" ? (
            <AlertCircle className="size-3.5" />
          ) : phase === "connecting" ? (
            <Loader2 className="size-3.5 animate-spin" />
          ) : isIncoming || isRingingOutgoing ? (
            <PhoneIncoming className="size-3.5 animate-pulse" />
          ) : (
            <Phone className="size-3.5" />
          )}
          {statusText}
        </p>
        {isTrulyConnected && (
          <p className="mt-1 text-lg font-mono font-bold tabular-nums text-foreground">
            {formatDuration(elapsedSeconds)}
          </p>
        )}
        {isTrulyConnected && isMuted && (
          <span className="mt-0.5 inline-flex items-center gap-1 rounded-full bg-error-tint text-error px-2.5 py-0.5 text-[11px] font-bold">
            <MicOff className="size-3" />
            {copy(lang, { ar: "مكتوم", en: "Muted", ur: "خاموش" })}
          </span>
        )}
      </div>

      <div className="mt-5 flex items-center justify-center gap-3">
        {isIncoming ? (
          <>
            <button
              type="button"
              onClick={onDecline}
              title={copy(lang, { ar: "رفض", en: "Decline", ur: "مسترد کریں" })}
              className="size-12 rounded-full bg-error-tint text-error grid place-items-center hover:opacity-80 transition-opacity"
            >
              <PhoneOff className="size-5" />
            </button>
            <button
              type="button"
              onClick={onAccept}
              disabled={callActionPending}
              title={copy(lang, { ar: "قبول", en: "Accept", ur: "قبول کریں" })}
              className="size-12 rounded-full bg-success-tint text-success grid place-items-center hover:opacity-80 transition-opacity disabled:opacity-50"
            >
              <Phone className="size-5" />
            </button>
          </>
        ) : (
          <>
            {phase === "connected" && (
              <button
                type="button"
                onClick={onToggleMute}
                title={copy(lang, { ar: "كتم/إلغاء كتم", en: "Mute/unmute", ur: "خاموش/آواز" })}
                className={`size-11 rounded-full border grid place-items-center transition-colors ${
                  isMuted ? "border-error/40 bg-error-tint text-error" : "border-border hover:bg-stone-100"
                }`}
              >
                {isMuted ? <MicOff className="size-4" /> : <Mic className="size-4" />}
              </button>
            )}
            <button
              type="button"
              onClick={onEnd}
              title={copy(lang, { ar: "إنهاء المكالمة", en: "End call", ur: "کال ختم کریں" })}
              className="size-11 rounded-full bg-error-tint text-error grid place-items-center hover:opacity-80 transition-opacity"
            >
              <PhoneOff className="size-4" />
            </button>
          </>
        )}
      </div>
    </div>
  );
}
