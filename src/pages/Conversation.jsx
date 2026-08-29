import { useEffect, useRef, useState } from "react";
import { Link, useLocation, useNavigate, useParams } from "react-router-dom";
import {
  ArrowUpRight,
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
  ShieldAlert,
  ExternalLink,
  MessageCircle,
} from "lucide-react";

import { useI18n } from "../lib/useI18n";
import { useAuth } from "../lib/useAuth";
import { getConversation, sendConversationMessage } from "../api/conversations";
import { getReport } from "../api/reports";
import { startCall, joinCall, endCall } from "../api/calls";
import { useAgoraCall } from "../hooks/useAgoraCall";
import { ReportType } from "../api/enums";
import { ApiError } from "../api/httpClient";

function copy(lang, values) {
  return values[lang] ?? values.en;
}

// The backend serializes timestamps as UTC ("...Z"), but a value re-fetched
// through EF Core loses that marker - SQL Server's datetime2 columns don't
// store timezone info, so EF Core always returns DateTimeKind.Unspecified
// on read, and the JSON then has no "Z"/offset at all (confirmed live: the
// immediate send-message response carries "Z", the very same message
// re-fetched via GET does not). Per the ECMAScript Date spec, a date-time
// string with no timezone designator parses as LOCAL time, not UTC - so an
// unmarked UTC value displayed exactly `browser-UTC-offset` hours off (3
// hours behind in Saudi Arabia, UTC+3) as soon as the page reloaded or the
// conversation was reopened, not just for "old" messages. Treat an
// unmarked string as UTC explicitly (it genuinely is) instead of trusting
// the browser's default local-time interpretation - never a hardcoded
// offset, the actual conversion still comes from toLocaleTimeString using
// the browser's own timezone.
function parseServerTime(iso) {
  const hasTimezone = /Z$|[+-]\d{2}:\d{2}$/.test(iso);
  return new Date(hasTimezone ? iso : `${iso}Z`);
}

function formatTime(iso) {
  if (!iso) return "";
  try {
    return parseServerTime(iso).toLocaleTimeString(undefined, { hour: "2-digit", minute: "2-digit" });
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
  const [linkedReport, setLinkedReport] = useState(null);
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

  // The safety handover warning is only for the person who actually created
  // the FOUND report. reportType alone is not enough because both
  // participants see the same conversation/report context.
  useEffect(() => {
    const reportId = conversation?.reportId;

    if (!reportId) {
      setLinkedReport(null);
      return;
    }

    let cancelled = false;

    getReport(reportId)
      .then((report) => {
        if (!cancelled) setLinkedReport(report);
      })
      .catch(() => {
        if (!cancelled) setLinkedReport(null);
      });

    return () => {
      cancelled = true;
    };
  }, [conversation?.reportId]);

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
      // Bug fix: this used to call setErrorMsg, which only ever renders on
      // the page's initial-load failure screen (status === "error") - once
      // the conversation itself has loaded, that state is never shown
      // again, so a failed call silently did nothing visible at all. This
      // is the actual live call-status area (see callNotice above), the
      // same one "Call ended"/"Missed call" already use.
      setCallNotice(
        err.message ||
          copy(lang, {
            ar: "تعذّر بدء المكالمة. حاول مرة أخرى.",
            en: "Couldn't start the call. Please try again.",
            ur: "کال شروع نہیں ہو سکی۔ دوبارہ کوشش کریں۔",
          })
      );
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
      // Same fix as handleStartCall above.
      setCallNotice(
        err.message ||
          copy(lang, {
            ar: "تعذّر الانضمام إلى المكالمة. حاول مرة أخرى.",
            en: "Couldn't join the call. Please try again.",
            ur: "کال میں شامل نہیں ہو سکے۔ دوبارہ کوشش کریں۔",
          })
      );
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

  const isFinderHoldingItem =
    conversation?.reportType === ReportType.FOUND &&
    Boolean(userId) &&
    Boolean(linkedReport?.creatorId) &&
    String(linkedReport.creatorId).toLowerCase() === String(userId).toLowerCase();

  return (
    <section
      dir={lang === "ar" || lang === "ur" ? "rtl" : "ltr"}
      className="min-h-[calc(100vh-80px)] bg-background py-6 sm:py-8 lg:py-10"
    >
      <div className="mx-auto max-w-5xl px-4 sm:px-6">
        {/* Back navigation */}
        <div className="mb-4">
          <Link
            to="/messages"
            className="inline-flex items-center gap-2 rounded-xl px-2 py-1.5 text-sm font-semibold text-muted-foreground transition-colors hover:bg-stone-100 hover:text-foreground"
          >
            <ArrowUpRight
              className={`size-3.5 ${
                lang === "ar" || lang === "ur" ? "" : "-scale-x-100"
              }`}
              strokeWidth={1.7}
            />
            {copy(lang, {
              ar: "العودة إلى الرسائل",
              en: "Back to messages",
              ur: "پیغامات پر واپس جائیں",
            })}
          </Link>
        </div>

        <div className={showCallCard ? "lg:flex lg:items-start lg:gap-5" : ""}>
          {/* Conversation shell */}
          <div
            className={`
              mx-auto flex w-full min-w-0 flex-col overflow-hidden
              rounded-[1.75rem] border border-border bg-card shadow-soft
              h-[calc(100vh-170px)] min-h-[34rem] max-h-[47rem]
              ${showCallCard ? "lg:flex-1" : "max-w-4xl"}
            `}
          >
            {/* Compact conversation header */}
            <header className="shrink-0 border-b border-border bg-card px-4 py-3.5 sm:px-5">
              <div className="flex items-center gap-3">
                <span className="grid size-11 shrink-0 place-items-center rounded-2xl bg-primary/10 font-mono text-sm font-extrabold text-primary">
                  {conversation.otherParticipantName?.slice(0, 2).toUpperCase() || "?"}
                </span>

                <div className="min-w-0 flex-1">
                  <div className="flex min-w-0 items-center gap-2">
                    <h1 className="truncate text-sm font-extrabold text-foreground sm:text-base">
                      {conversation.otherParticipantName ||
                        copy(lang, {
                          ar: "مستخدم لُقيا",
                          en: "Luqya user",
                          ur: "لقیا صارف",
                        })}
                    </h1>

                    <span
                      className={`
                        hidden shrink-0 rounded-md px-2 py-0.5 text-[10px] font-bold sm:inline-flex
                        ${
                          conversation.reportType === ReportType.FOUND
                            ? "bg-success-tint text-success"
                            : "bg-warn-tint text-warn"
                        }
                      `}
                    >
                      {conversation.reportType === ReportType.FOUND
                        ? copy(lang, {
                            ar: "بلاغ العثور",
                            en: "Found report",
                            ur: "ملنے کی رپورٹ",
                          })
                        : copy(lang, {
                            ar: "بلاغ الفقد",
                            en: "Lost report",
                            ur: "گمشدگی کی رپورٹ",
                          })}
                    </span>
                  </div>

                  <p className="mt-0.5 truncate text-xs text-muted-foreground">
                    {conversation.reportDescription ||
                      copy(lang, {
                        ar: "محادثة مرتبطة ببلاغ في لُقيا",
                        en: "Conversation linked to a Luqya report",
                        ur: "لقیا کی رپورٹ سے منسلک گفتگو",
                      })}
                  </p>
                </div>

                <div className="flex shrink-0 items-center gap-1.5">
                  <Link
                    to={`/match/${conversation.reportId}`}
                    title={copy(lang, {
                      ar: "عرض البلاغ",
                      en: "View report",
                      ur: "رپورٹ دیکھیں",
                    })}
                    className="hidden h-9 items-center gap-1.5 rounded-xl border border-border px-3 text-xs font-bold text-muted-foreground transition-colors hover:border-primary/25 hover:bg-primary/[0.035] hover:text-primary sm:inline-flex"
                  >
                    <ExternalLink className="size-3.5" />
                    {copy(lang, {
                      ar: "عرض البلاغ",
                      en: "View report",
                      ur: "رپورٹ دیکھیں",
                    })}
                  </Link>

                  <button
                    type="button"
                    onClick={handleStartCall}
                    disabled={
                      callActionPending ||
                      agoraCall.phase !== "idle" ||
                      Boolean(activeCall)
                    }
                    title={copy(lang, {
                      ar: "مكالمة صوتية",
                      en: "Voice call",
                      ur: "صوتی کال",
                    })}
                    className="
                      grid size-9 shrink-0 place-items-center rounded-xl
                      border border-border text-foreground/65
                      transition-all
                      hover:border-primary/30 hover:bg-primary/[0.04] hover:text-primary
                      disabled:cursor-not-allowed disabled:opacity-40
                    "
                  >
                    <Phone className="size-4" strokeWidth={1.8} />
                  </button>
                </div>
              </div>

              {/* Mobile report link */}
              <div className="mt-3 flex items-center justify-between gap-3 sm:hidden">
                <span
                  className={`
                    rounded-md px-2 py-0.5 text-[10px] font-bold
                    ${
                      conversation.reportType === ReportType.FOUND
                        ? "bg-success-tint text-success"
                        : "bg-warn-tint text-warn"
                    }
                  `}
                >
                  {conversation.reportType === ReportType.FOUND
                    ? copy(lang, {
                        ar: "بلاغ العثور",
                        en: "Found report",
                        ur: "ملنے کی رپورٹ",
                      })
                    : copy(lang, {
                        ar: "بلاغ الفقد",
                        en: "Lost report",
                        ur: "گمشدگی کی رپورٹ",
                      })}
                </span>

                <Link
                  to={`/match/${conversation.reportId}`}
                  className="inline-flex items-center gap-1 text-[11px] font-bold text-primary"
                >
                  <ExternalLink className="size-3" />
                  {copy(lang, {
                    ar: "عرض البلاغ",
                    en: "View report",
                    ur: "رپورٹ دیکھیں",
                  })}
                </Link>
              </div>
            </header>

            {!showCallCard && callNotice && (
              <div className="shrink-0 border-b border-border bg-stone-50 px-4 py-2.5 text-center text-xs font-semibold text-muted-foreground">
                {callNotice}
              </div>
            )}

            {/* Safety guidance — only for the creator of a FOUND report */}
            {isFinderHoldingItem && (
              <div className="shrink-0 border-b border-amber-200/60 bg-amber-50/65 px-4 py-3 sm:px-5">
              <div className="flex items-start gap-3">
                <span className="mt-0.5 grid size-8 shrink-0 place-items-center rounded-xl bg-amber-100 text-amber-700">
                  <ShieldAlert className="size-4" strokeWidth={1.9} />
                </span>

                <div className="min-w-0">
                  <p className="text-xs font-extrabold text-amber-950">
                    {copy(lang, {
                      ar: "تنبيه أمان قبل تسليم الغرض",
                      en: "Safety reminder before handing over an item",
                      ur: "چیز حوالے کرنے سے پہلے حفاظتی یاددہانی",
                    })}
                  </p>

                  <p className="mt-1 text-[11px] leading-5 text-amber-900/75 sm:text-xs">
                    {copy(lang, {
                      ar: "إذا كان الغرض بحوزتك، لا تكشف جميع العلامات المميزة أو التفاصيل الخاصة به. اطلب من الطرف الآخر وصف تفاصيل لا تظهر في البلاغ، وتأكد من تطابقها قبل التسليم.",
                      en: "If you have the item, don't reveal all of its identifying or private details. Ask the other person to describe details that aren't shown in the report, and verify them before handing it over.",
                      ur: "اگر چیز آپ کے پاس ہے تو اس کی تمام شناختی یا نجی تفصیلات ظاہر نہ کریں۔ دوسرے شخص سے وہ تفصیلات بیان کرنے کو کہیں جو رپورٹ میں نظر نہیں آتیں، اور چیز حوالے کرنے سے پہلے ان کی تصدیق کریں۔",
                    })}
                  </p>
                </div>
              </div>
            </div>
            )}

            {/* Messages */}
            <div className="flex-1 overflow-y-auto bg-stone-50/35 px-4 py-5 sm:px-6">
              {messages.length === 0 ? (
                <div className="flex h-full min-h-48 flex-col items-center justify-center px-6 text-center">
                  <span className="grid size-12 place-items-center rounded-2xl bg-primary/[0.07] text-primary">
                    <MessageCircle className="size-5" strokeWidth={1.7} />
                  </span>

                  <p className="mt-3 text-sm font-bold text-foreground">
                    {copy(lang, {
                      ar: "ابدأ المحادثة",
                      en: "Start the conversation",
                      ur: "گفتگو شروع کریں",
                    })}
                  </p>

                  <p className="mt-1 max-w-sm text-xs leading-5 text-muted-foreground">
                    {copy(lang, {
                      ar: "تحدث مع الطرف الآخر بخصوص الغرض، واحتفظ بالتواصل داخل لُقيا حتى يتم التحقق والتسليم.",
                      en: "Talk about the item here and keep communication inside Luqya until verification and handover are complete.",
                      ur: "چیز کے بارے میں یہاں بات کریں اور تصدیق و حوالگی مکمل ہونے تک رابطہ لقیا کے اندر رکھیں۔",
                    })}
                  </p>
                </div>
              ) : (
                <div className="space-y-3">
                  {messages.map((m) => (
                    <div
                      key={m.id}
                      className={`flex ${m.isMine ? "justify-end" : "justify-start"}`}
                    >
                      <div
                        className={`
                          max-w-[82%] px-4 py-2.5 text-sm shadow-[0_1px_2px_rgba(0,0,0,0.035)]
                          sm:max-w-[68%]
                          ${
                            m.isMine
                              ? "rounded-[1.2rem] rounded-ee-md bg-primary text-primary-foreground"
                              : "rounded-[1.2rem] rounded-es-md border border-border bg-card text-foreground"
                          }
                        `}
                      >
                        <p className="whitespace-pre-wrap break-words leading-6">
                          {m.text}
                        </p>

                        <div
                          className={`
                            mt-1.5 flex items-center justify-end gap-1 text-[10px]
                            ${
                              m.isMine
                                ? "text-primary-foreground/65"
                                : "text-muted-foreground"
                            }
                          `}
                        >
                          <span>{formatTime(m.creationTime)}</span>

                          {m.isMine &&
                            (m.isRead ? (
                              <CheckCheck className="size-3" />
                            ) : (
                              <Check className="size-3" />
                            ))}
                        </div>
                      </div>
                    </div>
                  ))}
                </div>
              )}

              <div ref={bottomRef} />
            </div>

            {/* Composer */}
            <footer className="shrink-0 border-t border-border bg-card p-3 sm:p-4">
              {conversation.reportIsClosed ? (
                <div className="flex items-center justify-center gap-2 rounded-xl bg-stone-50 px-4 py-3 text-sm text-muted-foreground">
                  <PackageCheck className="size-4" />
                  {copy(lang, {
                    ar: "هذا البلاغ مغلق. لا يمكن إرسال رسائل جديدة.",
                    en: "This report is closed. New messages can't be sent.",
                    ur: "یہ رپورٹ بند ہے۔ نئے پیغامات نہیں بھیجے جا سکتے۔",
                  })}
                </div>
              ) : (
                <form onSubmit={handleSend} className="flex items-center gap-2">
                  <div className="flex min-w-0 flex-1 items-center rounded-2xl border border-border bg-stone-50/80 transition-all focus-within:border-primary/35 focus-within:bg-card focus-within:ring-4 focus-within:ring-primary/[0.05]">
                    <input
                      type="text"
                      value={text}
                      onChange={(e) => setText(e.target.value)}
                      placeholder={copy(lang, {
                        ar: "اكتب رسالة...",
                        en: "Type a message...",
                        ur: "پیغام لکھیں...",
                      })}
                      className="h-12 min-w-0 flex-1 bg-transparent px-4 text-sm text-foreground outline-none placeholder:text-muted-foreground/70"
                    />
                  </div>

                  <button
                    type="submit"
                    disabled={!text.trim() || sending}
                    aria-label={copy(lang, {
                      ar: "إرسال",
                      en: "Send",
                      ur: "بھیجیں",
                    })}
                    title={copy(lang, {
                      ar: "إرسال",
                      en: "Send",
                      ur: "بھیجیں",
                    })}
                    className="
                      grid size-12 shrink-0 place-items-center rounded-2xl
                      bg-primary text-primary-foreground shadow-sm
                      transition-all
                      hover:-translate-y-0.5 hover:bg-primary/90 hover:shadow-md
                      disabled:pointer-events-none disabled:opacity-40 disabled:translate-y-0
                    "
                  >
                    {sending ? (
                      <Loader2 className="size-4 animate-spin" />
                    ) : (
                      <Send className="size-4" />
                    )}
                  </button>
                </form>
              )}
            </footer>
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
  const displayName =
    otherParticipantName ||
    copy(lang, {
      ar: "مستخدم لُقيا",
      en: "Luqya user",
      ur: "لقیا صارف",
    });

  const initials = displayName.slice(0, 2).toUpperCase();

  const state =
    phase === "error"
      ? "error"
      : isTrulyConnected
        ? "connected"
        : isIncoming || isRingingOutgoing
          ? "ringing"
          : "connecting";

  const stateStyle = {
    error: {
      dot: "bg-error",
      iconWrap: "bg-error-tint text-error",
      text: "text-error",
    },
    connected: {
      dot: "bg-success",
      iconWrap: "bg-success-tint text-success",
      text: "text-success",
    },
    ringing: {
      dot: "bg-primary",
      iconWrap: "bg-primary/10 text-primary",
      text: "text-primary",
    },
    connecting: {
      dot: "bg-primary",
      iconWrap: "bg-primary/10 text-primary",
      text: "text-primary",
    },
  }[state];

  const callLabel = copy(lang, {
    ar: "مكالمة صوتية",
    en: "Voice call",
    ur: "وائس کال",
  });

  const statusText = phase === "error"
    ? errorMessage ||
      copy(lang, {
        ar: "تعذّر الاتصال",
        en: "Call failed",
        ur: "کال ناکام ہو گئی",
      })
    : isIncoming
      ? copy(lang, {
          ar: "مكالمة واردة",
          en: "Incoming call",
          ur: "آنے والی کال",
        })
      : phase === "connecting"
        ? copy(lang, {
            ar: "جارٍ إنشاء الاتصال...",
            en: "Connecting...",
            ur: "رابطہ ہو رہا ہے...",
          })
        : isRingingOutgoing
          ? copy(lang, {
              ar: "بانتظار رد الطرف الآخر...",
              en: "Waiting for an answer...",
              ur: "جواب کا انتظار ہے...",
            })
          : copy(lang, {
              ar: "المكالمة متصلة",
              en: "Call connected",
              ur: "کال منسلک ہے",
            });

  return (
    <aside
      aria-label={callLabel}
      className="
        fixed left-1/2 top-20 z-40
        w-[calc(100%-2rem)] max-w-sm -translate-x-1/2
        overflow-hidden rounded-[1.5rem]
        border border-border/90 bg-card/95
        shadow-luxe backdrop-blur-xl
        animate-rise-in
        lg:static lg:left-auto lg:top-auto lg:z-auto
        lg:w-[18rem] lg:max-w-none lg:shrink-0 lg:translate-x-0
      "
    >
      {/* Compact identity header */}
      <div className="flex items-center gap-3 px-4 pb-3 pt-4">
        <span
          className="
            grid size-11 shrink-0 place-items-center rounded-xl
            bg-primary/[0.08] font-mono text-sm font-extrabold text-primary
            ring-1 ring-primary/10
          "
          aria-hidden="true"
        >
          {initials}
        </span>

        <div className="min-w-0 flex-1">
          <p className="truncate text-sm font-extrabold text-foreground">
            {displayName}
          </p>

          <div className="mt-0.5 flex items-center gap-1.5">
            <span
              className={`size-1.5 shrink-0 rounded-full ${stateStyle.dot} ${
                state === "ringing" || state === "connecting"
                  ? "animate-pulse"
                  : ""
              }`}
            />
            <span className="text-[11px] font-medium text-muted-foreground">
              {callLabel}
            </span>
          </div>
        </div>

        <span
          className={`grid size-8 shrink-0 place-items-center rounded-xl ${stateStyle.iconWrap}`}
          aria-hidden="true"
        >
          {phase === "error" ? (
            <AlertCircle className="size-4" strokeWidth={1.8} />
          ) : phase === "connecting" ? (
            <Loader2 className="size-4 animate-spin" strokeWidth={1.8} />
          ) : isIncoming || isRingingOutgoing ? (
            <PhoneIncoming className="size-4" strokeWidth={1.8} />
          ) : (
            <Phone className="size-4" strokeWidth={1.8} />
          )}
        </span>
      </div>

      {/* Call status */}
      <div className="border-y border-border/70 bg-stone-50/55 px-4 py-3">
        <div className="flex items-center justify-between gap-3">
          <p className={`min-w-0 text-xs font-semibold ${stateStyle.text}`}>
            {statusText}
          </p>

          {isTrulyConnected && (
            <time
              className="
                shrink-0 font-mono text-sm font-bold tabular-nums
                tracking-wide text-foreground
              "
              aria-label={copy(lang, {
                ar: "مدة المكالمة",
                en: "Call duration",
                ur: "کال کا دورانیہ",
              })}
            >
              {formatDuration(elapsedSeconds)}
            </time>
          )}
        </div>

        {isTrulyConnected && isMuted && (
          <div className="mt-2 flex items-center gap-1.5 text-[11px] font-semibold text-error">
            <MicOff className="size-3.5" strokeWidth={1.8} />
            {copy(lang, {
              ar: "الميكروفون مكتوم",
              en: "Microphone muted",
              ur: "مائیکروفون خاموش ہے",
            })}
          </div>
        )}
      </div>

      {/* Controls */}
      <div className="p-3.5">
        {isIncoming ? (
          <div className="grid grid-cols-2 gap-2.5">
            <button
              type="button"
              onClick={onDecline}
              disabled={callActionPending}
              aria-label={copy(lang, {
                ar: "رفض المكالمة",
                en: "Decline call",
                ur: "کال مسترد کریں",
              })}
              className="
                inline-flex h-11 items-center justify-center gap-2 rounded-xl
                border border-error/20 bg-card
                px-3 text-xs font-bold text-error
                transition-all
                hover:border-error/35 hover:bg-error-tint/50
                focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-error/15
                disabled:pointer-events-none disabled:opacity-50
              "
            >
              <PhoneOff className="size-4" strokeWidth={1.8} />
              {copy(lang, {
                ar: "رفض",
                en: "Decline",
                ur: "مسترد",
              })}
            </button>

            <button
              type="button"
              onClick={onAccept}
              disabled={callActionPending}
              aria-label={copy(lang, {
                ar: "قبول المكالمة",
                en: "Accept call",
                ur: "کال قبول کریں",
              })}
              className="
                inline-flex h-11 items-center justify-center gap-2 rounded-xl
                bg-primary px-3
                text-xs font-bold text-primary-foreground
                shadow-sm transition-all
                hover:bg-primary/90 hover:shadow-md
                focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary/20
                disabled:pointer-events-none disabled:opacity-50
              "
            >
              {callActionPending ? (
                <Loader2 className="size-4 animate-spin" />
              ) : (
                <Phone className="size-4" strokeWidth={1.8} />
              )}
              {copy(lang, {
                ar: "قبول",
                en: "Accept",
                ur: "قبول",
              })}
            </button>
          </div>
        ) : (
          <div className="flex items-center gap-2.5">
            {phase === "connected" && (
              <button
                type="button"
                onClick={onToggleMute}
                aria-pressed={isMuted}
                aria-label={copy(lang, {
                  ar: isMuted ? "إلغاء كتم الميكروفون" : "كتم الميكروفون",
                  en: isMuted ? "Unmute microphone" : "Mute microphone",
                  ur: isMuted ? "مائیکروفون کی آواز بحال کریں" : "مائیکروفون خاموش کریں",
                })}
                className={`
                  inline-flex h-11 flex-1 items-center justify-center gap-2
                  rounded-xl border px-3 text-xs font-bold
                  transition-all
                  focus-visible:outline-none focus-visible:ring-2
                  ${
                    isMuted
                      ? "border-error/20 bg-error-tint/50 text-error focus-visible:ring-error/15"
                      : "border-border bg-card text-foreground/75 hover:border-primary/20 hover:bg-primary/[0.035] hover:text-primary focus-visible:ring-primary/15"
                  }
                `}
              >
                {isMuted ? (
                  <MicOff className="size-4" strokeWidth={1.8} />
                ) : (
                  <Mic className="size-4" strokeWidth={1.8} />
                )}

                {copy(lang, {
                  ar: isMuted ? "إلغاء الكتم" : "كتم",
                  en: isMuted ? "Unmute" : "Mute",
                  ur: isMuted ? "آواز بحال" : "خاموش",
                })}
              </button>
            )}

            <button
              type="button"
              onClick={onEnd}
              disabled={callActionPending}
              aria-label={copy(lang, {
                ar: "إنهاء المكالمة",
                en: "End call",
                ur: "کال ختم کریں",
              })}
              className={`
                inline-flex h-11 items-center justify-center gap-2
                rounded-xl border border-error/20 bg-error-tint/45
                px-3 text-xs font-bold text-error
                transition-all
                hover:border-error/35 hover:bg-error-tint
                focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-error/15
                disabled:pointer-events-none disabled:opacity-50
                ${phase === "connected" ? "flex-1" : "w-full"}
              `}
            >
              <PhoneOff className="size-4" strokeWidth={1.8} />
              {copy(lang, {
                ar: "إنهاء",
                en: "End call",
                ur: "ختم کریں",
              })}
            </button>
          </div>
        )}
      </div>
    </aside>
  );
}

