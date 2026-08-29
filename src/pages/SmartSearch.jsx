import { useEffect, useRef, useState } from "react";
import { Link, useLocation } from "react-router-dom";
import { Search, Loader2, AlertCircle, MapPin, Calendar, Sparkles, RotateCcw, X, ImagePlus, ArrowUpRight } from "lucide-react";

import { useI18n } from "../lib/useI18n";
import { useAuth } from "../lib/useAuth";
import DammaMark from "../components/DammaMark";
import { aiSearch, imageFileToBase64 } from "../api/search";
import { getReport, reportImageUrl } from "../api/reports";
import { listMatches, getMyDismissedReportIds } from "../api/matches";
import { ReportType, MatchStatus } from "../api/enums";
import { fetchMyReports } from "../lib/myReports";
import { validateImageFile, ImageValidationReason } from "../lib/imageValidation";
import { reportHeadingTitle } from "../lib/reportTitle";
import { ApiError } from "../api/httpClient";

// Task: never surface a raw backend/framework/network string (e.g. "401
// Unauthorized", a .NET exception message, a bare fetch TypeError). 400s
// stay as-is — those are ABP validation messages or UserFriendlyException
// text, which is deliberately written to be shown to the end user (same
// convention Match.jsx/Contact.jsx already use for that status code).
// Everything else maps to a fixed, friendly, localized message instead of
// whatever the server/network layer happened to say.
function searchErrorMessage(err, tr) {
  if (err instanceof ApiError) {
    if (err.isUnauthorized) {
      return tr({
        ar: "انتهت صلاحية جلستك. الرجاء تسجيل الدخول مرة أخرى للمتابعة.",
        en: "Your session has expired. Please log in again to continue.",
        ur: "آپ کا سیشن ختم ہو گیا ہے۔ جاری رکھنے کے لیے دوبارہ لاگ ان کریں۔",
      });
    }
    if (err.status === 400 && err.message) {
      return err.message;
    }
    if (err.status === 0) {
      return tr({
        ar: "تعذّر الاتصال بالخادم. تحقق من اتصالك بالإنترنت وحاول مرة أخرى.",
        en: "Couldn't reach the server. Check your connection and try again.",
        ur: "سرور تک رسائی نہیں ہو سکی۔ اپنا انٹرنیٹ کنکشن چیک کریں اور دوبارہ کوشش کریں۔",
      });
    }
  }
  return tr({
    ar: "حدث خطأ أثناء البحث. الرجاء المحاولة مرة أخرى.",
    en: "Something went wrong while searching. Please try again.",
    ur: "تلاش کے دوران خرابی پیش آئی۔ براہ کرم دوبارہ کوشش کریں۔",
  });
}

// Task C: same message set as ReportLost.jsx/ReportFound.jsx — one rejection
// reason always reads the same way anywhere images are picked in this app.
function imageValidationMessage(tr, reason) {
  switch (reason) {
    case ImageValidationReason.TOO_LARGE:
      return tr({
        ar: "حجم الصورة كبير جدًا (الحد الأقصى 8 ميجابايت).",
        en: "That photo is too large (8 MB maximum).",
        ur: "یہ تصویر بہت بڑی ہے (زیادہ سے زیادہ 8 MB)۔",
      });
    case ImageValidationReason.INVALID_FORMAT:
      return tr({
        ar: "صيغة الصورة غير مدعومة. الرجاء استخدام JPEG أو PNG أو WEBP.",
        en: "That file isn't a supported image. Please use JPEG, PNG, or WEBP.",
        ur: "یہ فائل معاون تصویر نہیں ہے۔ براہ کرم JPEG، PNG، یا WEBP استعمال کریں۔",
      });
    default:
      return tr({
        ar: "تعذّر قراءة هذه الصورة. جرّب صورة أخرى.",
        en: "Couldn't read that photo. Please try a different file.",
        ur: "یہ تصویر پڑھی نہیں جا سکی۔ دوسری فائل آزمائیں۔",
      });
  }
}

async function enrichResultsWithReportType(items) {
  const checked = await Promise.all(
    (items ?? []).map(async (item) => {
      // Resolve the persisted Report.type once when results arrive. After
      // that, All / Lost / Found is a local filter and switching tabs is
      // immediate instead of running another AI search.
      let actualType = item?.type ?? item?.reportType;

      if (actualType == null && item?.reportId) {
        try {
          const report = await getReport(item.reportId);
          actualType = report?.type;
        } catch {
          return { ...item, type: null };
        }
      }

      return { ...item, type: actualType ?? null };
    })
  );

  return checked.filter(Boolean);
}

export default function SmartSearch() {
  const { t, tr } = useI18n();
  const { userId } = useAuth();
  const location = useLocation();
  const restoredSearch = location.state?.restoreSmartSearch ?? null;

  const [text, setText] = useState("");
  const [status, setStatus] = useState(
    () => restoredSearch?.status ?? "idle"
  ); // idle | loading | success | empty | error
  const [results, setResults] = useState(
    () => restoredSearch?.results ?? []
  );
  const [errorMsg, setErrorMsg] = useState(null);
  const [sessionExpired, setSessionExpired] = useState(false);

  // Conversational search (Task: preserve context across messages) - context
  // is the previous turn's extracted {type, description, color, location} —
  // a single concise current value each, echoed back on the next search so
  // ai_service can combine it with the new message/image; it is never the
  // full conversation text. history is the visible chat log (user turns +
  // assistant replies/follow-up prompts) rendered inside the interaction
  // box. Both live only in this component's state - no persistence, no
  // server session, cleared on navigation away from the page.
  const [context, setContext] = useState(
    () => restoredSearch?.context ?? null
  );
  const [history, setHistory] = useState(
    () => restoredSearch?.history ?? []
  );

  const [ownReportsExcluded, setOwnReportsExcluded] = useState(
    () => restoredSearch?.ownReportsExcluded ?? 0
  );
  // Phase 4 Part 4: mirrors ownReportsExcluded for the dismissed-pair
  // filter (Phase 4 Part 3) — see runSearch and the "empty" state render
  // below for why this needs its own counter, not just a shared one.
  const [dismissedExcluded, setDismissedExcluded] = useState(
    () => restoredSearch?.dismissedExcluded ?? 0
  );

  // Task 2 (Phase 3 Part 3): whether the search that produced the current
  // "empty" state had an image and no text. Live-diagnosed root cause: a
  // pure image-only query, when no image-understanding provider is
  // currently reachable (no API key configured, Ollama not running), has
  // no signal to fall back to at all (unlike a combined query, which still
  // has text to score on) and correctly-but-unhelpfully returns zero
  // results — indistinguishable, from the empty state alone, from "no
  // similar items exist". Tracked so the empty state can honestly nudge
  // toward the one thing this session measured as reliably working
  // (adding text), without fabricating a specific diagnosis the frontend
  // has no way to actually confirm.
  const [lastSearchWasImageOnly, setLastSearchWasImageOnly] = useState(
    () => restoredSearch?.lastSearchWasImageOnly ?? false
  );

  // Task C — Smart Image Search: image is entirely additive to the existing
  // text search, not a separate feature/endpoint (see the request contract
  // note below).
  const [imageFile, setImageFile] = useState(null);
  const [imagePreview, setImagePreview] = useState(null);
  const [imageError, setImageError] = useState(null);
  const [isDragOver, setIsDragOver] = useState(false);
  const fileInputRef = useRef(null);
  const conversationRef = useRef(null);
  const resultsRef = useRef(null);
  // Task: Retry must safely repeat the same request. text/imageFile are
  // already cleared by the time a response comes back (chat-style "clear
  // the box after sending"), so the Retry button can't just re-invoke
  // runSearch — it replays the exact params that were actually sent.
  const lastRequestRef = useRef(null);

  useEffect(() => {
    document.title = tr({ ar: "البحث الذكي — لُقيا", en: "Smart search — Luqya", ur: "ذہین تلاش — لقیا" });
  }, [tr]);

  useEffect(() => {
    return () => {
      if (imagePreview) URL.revokeObjectURL(imagePreview);
    };
  }, [imagePreview]);

  // Keep the composer in a stable position while the conversation grows.
  // New messages scroll inside the conversation area instead of pushing the
  // search box farther down the page.
  useEffect(() => {
    const node = conversationRef.current;
    if (!node) return;

    const frame = window.requestAnimationFrame(() => {
      node.scrollTo({ top: node.scrollHeight, behavior: "smooth" });
    });

    return () => window.cancelAnimationFrame(frame);
  }, [history.length, status]);

  // When fresh matches arrive, gently bring the results into view so they
  // don't appear below the fold without the user noticing.
  useEffect(() => {
    if (status !== "success" || results.length === 0) return undefined;

    const timer = window.setTimeout(() => {
      resultsRef.current?.scrollIntoView({
        behavior: "smooth",
        block: "start",
      });
    }, 140);

    return () => window.clearTimeout(timer);
  }, [results.length, status]);

  async function handleImageFile(file) {
    if (!file) {
      if (imagePreview) URL.revokeObjectURL(imagePreview);
      setImageFile(null);
      setImagePreview(null);
      setImageError(null);
      return;
    }

    const reason = await validateImageFile(file);
    if (reason) {
      setImageError(imageValidationMessage(tr, reason));
      return;
    }

    if (imagePreview) URL.revokeObjectURL(imagePreview);
    setImageError(null);
    setImageFile(file);
    setImagePreview(URL.createObjectURL(file));
  }

  function handleDrop(event) {
    event.preventDefault();
    setIsDragOver(false);
    const file = event.dataTransfer.files?.[0];
    if (file) handleImageFile(file);
  }

  async function runSearch(event) {
    event?.preventDefault();
    // Matches AiSearchAppService.SearchAsync's own rule server-side ("Provide
    // a description, an image, or both to search.") — image-only, text-only,
    // and combined searches are all valid; only both-empty is rejected.
    if (!text.trim() && !imageFile) return;

    // Render the user's own turn immediately, then clear the box for the
    // next message — standard chat behavior (Task A: "clear the textarea
    // after sending"). imageFile/text below still refer to what was just
    // sent, since clearing only schedules the next render.
    setHistory((prev) => [
      ...prev,
      {
        role: "user",
        text:
          text.trim() ||
          tr({ ar: "📷 صورة مرفقة", en: "📷 Attached image", ur: "📷 منسلک تصویر" }),
      },
    ]);

    const reqText = text.trim() || undefined;
    const reqImageFile = imageFile;
    const reqContext = context;
    const isImageOnly = Boolean(reqImageFile) && !reqText;

    setText("");
    handleImageFile(null);

    // Verified request contract (AiSearchInputDto): Text and ImageBase64
    // are independently optional, both go to the SAME POST
    // api/app/ai-search/search endpoint / AiMatchingService scoring path
    // that text-only search already used — no new/parallel endpoint (Task
    // E2). Only send `type` when a specific one is selected —
    // AiSearchInputDto.Type is nullable on the backend specifically so
    // omitting it searches both.
    const imageBase64 = reqImageFile ? await imageFileToBase64(reqImageFile) : undefined;
    const requestArgs = {
      text: reqText,
      imageBase64,
      // Let the conversational search infer the user's intent from natural
      // language (for example: "وجدت" / "لقيت" vs "فقدت" / "ضيعت").
      // The backend then searches the opposite report side automatically.
      type: undefined,
      context: reqContext,
      isImageOnly,
    };
    lastRequestRef.current = requestArgs;

    await performSearch(requestArgs);
  }

  // Replays the exact last-submitted request — no new chat bubble (retry is
  // not a new user message), no dependency on text/imageFile state (already
  // cleared).
  async function retrySearch() {
    if (!lastRequestRef.current) return;
    await performSearch(lastRequestRef.current);
  }

  async function performSearch(
    { text: reqText, imageBase64, type, context: reqContext, isImageOnly },
    { appendAssistant = true, updateContext = true, silentLoading = false } = {}
  ) {
    if (!silentLoading) setStatus("loading");
    setErrorMsg(null);
    setSessionExpired(false);
    setOwnReportsExcluded(0);
    setDismissedExcluded(0);
    setLastSearchWasImageOnly(isImageOnly);

    try {
      const data = await aiSearch({
        text: reqText,
        imageBase64,
        type,
        maxResults: 12,
        context: reqContext,
      });

      // The assistant's turn in the chat log — reply covers the greeting/
      // incomplete/complete-search cases, followUpPrompt covers the image-
      // search "here are results, want to add a location?" case. The two
      // are mutually exclusive today, but either one (never both being
      // needed at once) must reach the log so a real reply is never hidden
      // just because results happened to come back empty.
      const assistantText = data.reply || data.followUpPrompt || null;
      if (assistantText && appendAssistant) {
        setHistory((prev) => [...prev, { role: "assistant", text: assistantText }]);
      }

      // Only overwrite context when this turn actually extracted something —
      // a bare greeting extracts nothing and must not wipe out an item
      // already described in an earlier turn.
      if (
        updateContext &&
        (data.extractedType || data.extractedDescription || data.extractedColor || data.extractedLocation)
      ) {
        setContext({
          type: data.extractedType || null,
          description: data.extractedDescription || null,
          color: data.extractedColor || null,
          location: data.extractedLocation || null,
          // Once a role ("lost"/"found") is confirmed by any turn, keep it
          // for later turns that don't restate it (e.g. a bare "في المول")
          // — the backend already resolves/falls back to this itself, this
          // just carries that resolved value forward instead of dropping
          // it if a later turn's response ever omits it.
          reportKind: data.reportKind || reqContext?.reportKind || null,
          // Same reasoning as reportKind - once the item's original-
          // language name is known, keep using it verbatim rather than
          // letting a later turn silently drop it (see search.js's remarks
          // on why this affects match quality, not just wording).
          itemNameLocal: data.itemNameLocal || reqContext?.itemNameLocal || null,
        });
      }

      let filtered = data.results ?? [];

      // Resolve each result's real persisted type once. Switching the visible
      // filter after this point is synchronous and does not call the backend.
      filtered = await enrichResultsWithReportType(filtered);

      // Exclude the current user's own reports from recovery candidates —
      // only when ownership can be verified from real data. AiSearchResultDto
      // has no ownership field of its own, so this cross-references against
      // the current user's own reports (resolved via Report.CreatorId in
      // fetchMyReports) — not a cached, session-only guess.
      if (userId) {
        let dismissedCount = 0;

        // fetchMyReports (+ its dependent listMatches lookup below) and
        // getMyDismissedReportIds are independent of each other - fetching
        // them in parallel instead of one-after-another (as this used to)
        // removes a full extra round trip from the tail end of every
        // search, after the AI response (and its chat reply) has already
        // arrived - see the loading-state note below.
        const [mine, directDismissedIds] = await Promise.all([
          fetchMyReports({ userId }),
          getMyDismissedReportIds().catch(() => []),
        ]);

        if (mine.reliable && mine.reports.length > 0) {
          const myIds = new Set(mine.reports.map((r) => r.id));
          const before = filtered.length;
          filtered = filtered.filter((r) => !myIds.has(r.reportId));
          setOwnReportsExcluded(before - filtered.length);

          // Phase 4 Part 3 (Task A.4/decision #3), superseded as the only
          // mechanism by Phase 4 Part 8 (see below) but kept for backward
          // compatibility with dismissals recorded before that redesign:
          // a "not my item" click that happened to go through the old
          // has-own-report path persisted as a real MatchStatus.Rejected
          // row — this cross-references those against the user's own
          // reports, the same real-data pattern as the own-reports
          // exclusion just above, not a session-local list.
          try {
            const matchesRes = await listMatches({ maxResultCount: 200, sorting: "creationTime desc" });
            const dismissedReportIds = new Set(
              (matchesRes?.items ?? [])
                .filter(
                  (m) =>
                    m.status === MatchStatus.REJECTED &&
                    (myIds.has(m.lostReportId) || myIds.has(m.foundReportId))
                )
                .map((m) => (myIds.has(m.lostReportId) ? m.foundReportId : m.lostReportId))
            );
            if (dismissedReportIds.size > 0) {
              const beforeDismissed = filtered.length;
              filtered = filtered.filter((r) => !dismissedReportIds.has(r.reportId));
              dismissedCount += beforeDismissed - filtered.length;
            }
          } catch {
            // Non-fatal: if the matches list can't be fetched, dismissed
            // candidates just aren't re-filtered for this one search — the
            // own-reports exclusion above (independent of this call)
            // still applies.
          }
        }

        // Phase 4 Part 8 (Task B, point 4): the real, own-report-independent
        // exclusion — reports the user has directly recorded a "not my
        // item" disposition toward (ReportClaim.IsMine == false), via
        // MatchAppService.GetMyDismissedReportIdsAsync. Runs for EVERY
        // authenticated user, not gated on owning any report at all —
        // this is the mechanism that actually closes the gap the old,
        // Match-based exclusion above could never handle (a user with
        // zero reports dismissing a result and it genuinely never
        // resurfacing for them). Fetched above, in parallel with
        // fetchMyReports; a failed fetch resolves to [] there (non-fatal,
        // same reasoning as the Match-based exclusion above).
        if (directDismissedIds?.length > 0) {
          const dismissedSet = new Set(directDismissedIds);
          const beforeDirect = filtered.length;
          filtered = filtered.filter((r) => !dismissedSet.has(r.reportId));
          dismissedCount += beforeDirect - filtered.length;
        }

        setDismissedExcluded(dismissedCount);
      }

      if (!data.shouldMatch) {
        // Reply-only turn (greeting or incomplete description) — no search
        // ran, so this must never render as "no matching reports."
        setResults([]);
        setStatus("reply");
      } else {
        // Keep the result set exactly as the intent-aware search returned it.
        // No manual Lost / Found filter is needed in the UI.
        setResults(filtered);
        setStatus(filtered.length > 0 ? "success" : "empty");
      }
    } catch (err) {
      setStatus("error");
      setSessionExpired(err instanceof ApiError && err.isUnauthorized);
      setErrorMsg(searchErrorMessage(err, tr));
    }
  }

  const hasSearched = history.length > 0 || status !== "idle";
  const isBusy = status === "loading";

  const smartSearchState = {
    status,
    results,
    context,
    history,
    ownReportsExcluded,
    dismissedExcluded,
    lastSearchWasImageOnly,
  };

  return (
    <section
      className="relative overflow-hidden py-9 sm:py-11 lg:py-14"
      onDragOver={(e) => {
        e.preventDefault();
        setIsDragOver(true);
      }}
      onDragLeave={() => setIsDragOver(false)}
      onDrop={handleDrop}
    >
      <div className="pointer-events-none absolute inset-x-0 top-0 -z-10 h-56 bg-gradient-to-b from-primary/[0.025] to-transparent" />

      <div className="mx-auto max-w-4xl px-4 sm:px-6">
        {!hasSearched ? (
          <>
            <header className="mx-auto mb-8 max-w-2xl text-center animate-fade-up sm:mb-10">
              <div className="mb-4 inline-flex items-center gap-2 rounded-full border border-primary/10 bg-primary/[0.035] px-3 py-1.5 text-[10px] font-bold tracking-[0.14em] text-primary">
                <DammaMark className="size-3.5" />
                {t("navSearch")}
              </div>

              <h1 className="font-display text-3xl font-extrabold tracking-tight text-foreground sm:text-4xl lg:text-5xl">
                {t("searchTitle")}
              </h1>

              <p className="mx-auto mt-4 max-w-xl text-sm leading-7 text-muted-foreground sm:text-base sm:leading-8">
                {tr({
                  ar: "صف ما حدث باختصار، وسيتولى لُقيا تحديد نوع البحث المناسب وعرض البلاغات الأكثر صلة. ويمكنك إضافة صورة بشكل اختياري لتحسين دقة النتائج.",
                  en: "Briefly describe what happened. Luqya will determine the right search direction and show the most relevant reports. You can optionally add a photo to improve accuracy.",
                  ur: "مختصراً بتائیں کیا ہوا۔ لقیا مناسب تلاش کی سمت خود طے کرے گا اور سب سے متعلقہ رپورٹس دکھائے گا۔ بہتر نتائج کے لیے تصویر شامل کرنا اختیاری ہے۔",
                })}
              </p>
            </header>

            <SearchComposer
              compact={false}
              text={text}
              setText={setText}
              isBusy={isBusy}
              imageFile={imageFile}
              imagePreview={imagePreview}
              imageError={imageError}
              isDragOver={isDragOver}
              fileInputRef={fileInputRef}
              onImageFile={handleImageFile}
              onDrop={handleDrop}
              onSubmit={runSearch}
              t={t}
              tr={tr}
            />
          </>
        ) : (
          <>
            <div className="mb-5 flex items-center gap-2.5 animate-fade-up">
              <span className="grid size-8 place-items-center rounded-full border border-primary/10 bg-primary/[0.05] text-primary">
                <DammaMark className="size-3.5" />
              </span>
              <div>
                <p className="text-sm font-bold text-foreground">{t("navSearch")}</p>
                <p className="mt-0.5 text-[11px] text-muted-foreground">
                  {tr({
                    ar: "أضف أي تفصيل جديد وسنحدّث النتائج مباشرة.",
                    en: "Add any new detail and we’ll refine the results right away.",
                    ur: "کوئی نئی تفصیل شامل کریں، ہم فوراً نتائج بہتر کر دیں گے۔",
                  })}
                </p>
              </div>
            </div>

            <div className="overflow-hidden rounded-[1.8rem] border border-border/80 bg-card shadow-soft animate-fade-up">
              {(history.length > 0 || status === "loading") && (
                <div className="border-b border-border/70 bg-stone-50/[0.22] px-4 py-4 sm:px-5 sm:py-5">
                  <div className="mb-3 flex items-center justify-between gap-3">
                    <p className="text-[11px] font-semibold text-muted-foreground">
                      {tr({ ar: "المحادثة", en: "Conversation", ur: "گفتگو" })}
                    </p>
                    <span className="text-[10px] text-muted-foreground/75">
                      {tr({
                        ar: "نحتفظ بالسياق لتحسين البحث",
                        en: "Context is kept to improve the search",
                        ur: "بہتر تلاش کے لیے سیاق محفوظ رہتا ہے",
                      })}
                    </span>
                  </div>

                  <div
                    ref={conversationRef}
                    className="flex h-64 flex-col gap-4 overflow-y-auto pe-2 py-1 scroll-smooth sm:h-72"
                  >
                    {history.map((entry, index) => {
                      const isUser = entry.role === "user";
                      return (
                        <div
                          key={`${entry.role}-${index}`}
                          className={`flex animate-fade-up ${isUser ? "justify-end" : "justify-start"}`}
                        >
                          <div
                            className={`max-w-[88%] sm:max-w-[78%] ${
                              isUser ? "text-end" : "text-start"
                            }`}
                          >
                            <p className="mb-1 px-1 text-[10px] font-medium text-muted-foreground">
                              {isUser
                                ? tr({ ar: "أنت", en: "You", ur: "آپ" })
                                : tr({ ar: "لُقيا", en: "Luqya", ur: "لقیا" })}
                            </p>

                            <div
                              className={`rounded-2xl border px-4 py-3 text-sm leading-7 shadow-[0_1px_2px_rgba(0,0,0,0.02)] ${
                                isUser
                                  ? "rounded-ee-md border-primary/10 bg-primary/[0.055] text-foreground"
                                  : "rounded-es-md border-border bg-background text-foreground/90"
                              }`}
                            >
                              {entry.text}
                            </div>
                          </div>
                        </div>
                      );
                    })}

                    {status === "loading" && <AssistantTypingIndicator tr={tr} />}
                  </div>
                </div>
              )}

              <div className="p-4 sm:p-5">
                <SearchComposer
                  compact
                  text={text}
                  setText={setText}
                  isBusy={isBusy}
                  imageFile={imageFile}
                  imagePreview={imagePreview}
                  imageError={imageError}
                  isDragOver={isDragOver}
                  fileInputRef={fileInputRef}
                  onImageFile={handleImageFile}
                  onDrop={handleDrop}
                  onSubmit={runSearch}
                  t={t}
                  tr={tr}
                />
              </div>

            </div>

            {status === "error" && (
              <div className="mt-5 rounded-2xl border border-error/15 bg-error-tint/55 px-4 py-4 animate-fade-up">
                <div className="flex items-start gap-3">
                  <span className="mt-0.5 grid size-8 shrink-0 place-items-center rounded-full bg-background text-error">
                    <AlertCircle className="size-4" />
                  </span>
                  <div className="min-w-0 flex-1">
                    <p className="text-sm leading-6 text-error">{errorMsg}</p>

                    {sessionExpired ? (
                      <Link
                        to="/auth/login"
                        className="mt-2 inline-flex text-sm font-semibold text-primary hover:underline"
                      >
                        {tr({ ar: "تسجيل الدخول", en: "Log in", ur: "لاگ ان کریں" })}
                      </Link>
                    ) : (
                      <button
                        type="button"
                        onClick={retrySearch}
                        className="mt-2 inline-flex items-center gap-2 text-sm font-semibold text-primary hover:underline"
                      >
                        <RotateCcw className="size-3.5" />
                        {t("searchRetry")}
                      </button>
                    )}
                  </div>
                </div>
              </div>
            )}

            {status === "empty" && (
              <SearchEmptyState
                t={t}
                tr={tr}
                ownReportsExcluded={ownReportsExcluded}
                dismissedExcluded={dismissedExcluded}
                lastSearchWasImageOnly={lastSearchWasImageOnly}
              />
            )}

            {status === "success" && (
              <div ref={resultsRef} className="scroll-mt-24">
                <SearchResults
                  results={results}
                  ownReportsExcluded={ownReportsExcluded}
                  dismissedExcluded={dismissedExcluded}
                  smartSearchState={smartSearchState}
                  t={t}
                  tr={tr}
                />
              </div>
            )}
          </>
        )}
      </div>
    </section>
  );
}

/* -------------------------------------------------------------------------- */
/*                               DESIGN ONLY                                  */
/* -------------------------------------------------------------------------- */

function SearchComposer({
  compact,
  text,
  setText,
  isBusy,
  imageFile,
  imagePreview,
  imageError,
  isDragOver,
  fileInputRef,
  onImageFile,
  onDrop,
  onSubmit,
  t,
  tr,
}) {
  return (
    <form
      onSubmit={onSubmit}
      className={`relative overflow-hidden transition-all duration-300 ${
        compact
          ? "bg-transparent"
          : `border bg-card shadow-soft ${
              isDragOver
                ? "border-primary/45 ring-4 ring-primary/[0.06]"
                : "border-border"
            } rounded-[2rem] p-5 animate-fade-up sm:p-6 lg:p-7`
      }`}
    >
      {!compact && (
        <div className="pointer-events-none absolute inset-x-8 top-0 h-px bg-gradient-to-r from-transparent via-primary/25 to-transparent" />
      )}

      <div className="relative">
        <textarea
          value={text}
          onChange={(e) => setText(e.target.value)}
          placeholder={
            compact
              ? tr({
                  ar: "أضف تفصيلًا آخر... مثل اللون أو المكان أو علامة مميزة",
                  en: "Add another detail... such as color, location, or a distinctive mark",
                  ur: "مزید تفصیل شامل کریں... جیسے رنگ، مقام یا کوئی نمایاں نشان",
                })
              : t("searchPh")
          }
          rows={compact ? 2 : 4}
          className={`w-full resize-none rounded-[1.35rem] border border-stone-200/90 px-5 py-4 text-[15px] leading-7 text-foreground transition-all placeholder:text-muted-foreground/65 focus:border-primary/45 focus:bg-background focus:outline-none focus:ring-4 focus:ring-primary/[0.055] ${
            compact ? "bg-background" : "bg-stone-50/80"
          }`}
        />
      </div>

      <div className="mt-3 flex min-h-9 items-center">
        {imagePreview ? (
          <div className="inline-flex max-w-full items-center gap-2 rounded-xl border border-border bg-background px-2 py-1.5 shadow-sm">
            <img
              src={imagePreview}
              alt=""
              className="size-8 shrink-0 rounded-lg object-cover"
            />

            <span className="max-w-[170px] truncate text-xs font-medium text-foreground/70">
              {imageFile?.name}
            </span>

            <span className="mx-0.5 h-4 w-px bg-border" />

            <button
              type="button"
              onClick={() => fileInputRef.current?.click()}
              title={t("searchImageReplaceCta")}
              aria-label={t("searchImageReplaceCta")}
              className="grid size-7 place-items-center rounded-lg text-muted-foreground transition-colors hover:bg-primary/[0.05] hover:text-primary"
            >
              <RotateCcw className="size-3.5" />
            </button>

            <button
              type="button"
              onClick={() => onImageFile(null)}
              title={t("searchImageRemoveCta")}
              aria-label={t("searchImageRemoveCta")}
              className="grid size-7 place-items-center rounded-lg text-muted-foreground transition-colors hover:bg-error-tint hover:text-error"
            >
              <X className="size-3.5" />
            </button>
          </div>
        ) : (
          <button
            type="button"
            onClick={() => fileInputRef.current?.click()}
            onDragOver={(e) => e.preventDefault()}
            onDrop={onDrop}
            className="inline-flex items-center gap-2 rounded-xl px-2.5 py-2 text-xs font-semibold text-muted-foreground transition-colors hover:bg-primary/[0.045] hover:text-primary"
          >
            <ImagePlus className="size-4" strokeWidth={1.7} />
            <span>{t("searchByImageLabel")}</span>
            <span className="rounded-full border border-border bg-background px-2 py-0.5 text-[10px] font-medium text-muted-foreground">
              {tr({ ar: "اختياري", en: "Optional", ur: "اختیاری" })}
            </span>
          </button>
        )}

        <input
          ref={fileInputRef}
          type="file"
          accept="image/*"
          onChange={(e) => onImageFile(e.target.files?.[0] ?? null)}
          className="hidden"
        />
      </div>

      {imageError && (
        <div className="mt-2 flex items-start gap-2 rounded-xl bg-error-tint/60 px-3 py-2.5 text-xs leading-relaxed text-error">
          <AlertCircle className="mt-0.5 size-3.5 shrink-0" />
          <span>{imageError}</span>
        </div>
      )}

      <div className="mt-4 flex flex-col gap-3 border-t border-border/70 pt-4 sm:flex-row sm:items-center sm:justify-between">
        <div className="flex max-w-xl items-start gap-2.5 text-xs leading-6 text-muted-foreground">
          <span className="mt-1 grid size-6 shrink-0 place-items-center rounded-full bg-primary/[0.055] text-primary">
            <Sparkles className="size-3.5" strokeWidth={1.7} />
          </span>
          <p>
            {tr({
              ar: "يفهم لُقيا من وصفك ما إذا كنت فقدت غرضًا أو عثرت عليه، ثم يوجّه البحث تلقائيًا إلى البلاغات المناسبة.",
              en: "Luqya understands from your description whether you lost or found an item, then automatically searches the appropriate reports.",
              ur: "لقیا آپ کی تفصیل سے سمجھتا ہے کہ چیز گم ہوئی ہے یا ملی ہے، پھر خودکار طور پر مناسب رپورٹس میں تلاش کرتا ہے۔",
            })}
          </p>
        </div>

        <button
          type="submit"
          disabled={isBusy || (!text.trim() && !imageFile)}
          className="inline-flex min-h-11 shrink-0 items-center justify-center gap-2 rounded-xl bg-primary px-6 text-sm font-semibold text-primary-foreground shadow-sm transition-all hover:-translate-y-0.5 hover:shadow-md active:translate-y-0 active:scale-[0.99] disabled:pointer-events-none disabled:translate-y-0 disabled:opacity-45"
        >
          {isBusy ? (
            <Loader2 className="size-4 animate-spin" />
          ) : (
            <Search className="size-4" />
          )}

          {isBusy
            ? imageFile
              ? t("searchingByImageLabel")
              : t("searchLoading")
            : compact
              ? tr({ ar: "إرسال", en: "Send", ur: "بھیجیں" })
              : tr({ ar: "بحث", en: "Search", ur: "تلاش" })}
        </button>
      </div>

    </form>
  );
}

function AssistantTypingIndicator({ tr }) {
  return (
    <div className="mt-3 flex flex-col items-start animate-fade-up">
      <span className="mb-1 px-1 text-[10px] font-medium text-muted-foreground">
        {tr({ ar: "لُقيا", en: "Luqya", ur: "لقیا" })}
      </span>
      <div className="inline-flex items-center gap-2 rounded-[1.1rem] rounded-es-md border border-border bg-background px-4 py-3">
        <div className="inline-flex items-center gap-1.5">
          {[0, 1, 2].map((i) => (
            <span
              key={i}
              className="size-1.5 rounded-full bg-muted-foreground/45 animate-pulse"
              style={{ animationDelay: `${i * 150}ms` }}
            />
          ))}
        </div>
        <span className="text-[11px] text-muted-foreground">
          {tr({ ar: "جارٍ البحث...", en: "Searching...", ur: "تلاش جاری ہے..." })}
        </span>
      </div>
    </div>
  );
}

function SearchEmptyState({
  t,
  tr,
  ownReportsExcluded,
  dismissedExcluded,
  lastSearchWasImageOnly,
}) {
  return (
    <div className="mt-5 rounded-2xl border border-dashed border-border bg-background/70 px-5 py-7 text-center animate-fade-up sm:px-7">
      <div className="mx-auto grid size-10 place-items-center rounded-full bg-stone-100 text-muted-foreground">
        <Search className="size-4" />
      </div>

      {ownReportsExcluded > 0 || dismissedExcluded > 0 ? (
        <>
          <p className="mt-3 text-sm font-semibold text-foreground/80">
            {t("searchAllExcludedNote")}
          </p>

          {ownReportsExcluded > 0 && (
            <p className="mt-1.5 text-xs leading-relaxed text-muted-foreground">
              {t("searchOwnExcludedNote")}
            </p>
          )}

          {dismissedExcluded > 0 && (
            <p className="mt-1.5 text-xs leading-relaxed text-muted-foreground">
              {t("searchDismissedExcludedNote")}
            </p>
          )}
        </>
      ) : (
        <>
          <p className="mt-3 text-sm font-semibold text-foreground">
            {t("searchEmpty")}
          </p>
          <p className="mx-auto mt-1.5 max-w-md text-xs leading-relaxed text-muted-foreground">
            {tr({
              ar: "جرّب إضافة لون، موقع، علامة مميزة أو صورة لزيادة دقة النتائج.",
              en: "Try adding a color, location, distinctive detail, or photo to improve the results.",
              ur: "نتائج بہتر کرنے کے لیے رنگ، مقام، نمایاں تفصیل یا تصویر شامل کریں۔",
            })}
          </p>
        </>
      )}

      {lastSearchWasImageOnly && (
        <p className="mx-auto mt-2 max-w-md text-xs leading-relaxed text-muted-foreground">
          {t("searchEmptyImageOnlyHint")}
        </p>
      )}
    </div>
  );
}

function SearchResults({
  results,
  ownReportsExcluded,
  dismissedExcluded,
  smartSearchState,
  t,
  tr,
}) {
  const [topMatch, ...secondary] = results ?? [];

  const knownTypes = new Set(
    (results ?? [])
      .map((item) => Number(item?.type ?? item?.reportType))
      .filter((value) => Number.isFinite(value))
  );

  const onlyLostReports =
    knownTypes.size === 1 && knownTypes.has(Number(ReportType.LOST));
  const onlyFoundReports =
    knownTypes.size === 1 && knownTypes.has(Number(ReportType.FOUND));

  const resultTitle = onlyLostReports
    ? tr({
        ar: "بلاغات عن أغراض مفقودة",
        en: "Reported lost items",
        ur: "گمشدہ اشیا کی رپورٹس",
      })
    : onlyFoundReports
      ? tr({
          ar: "بلاغات عن أغراض تم العثور عليها",
          en: "Reported found items",
          ur: "ملنے والی اشیا کی رپورٹس",
        })
      : tr({
          ar: "النتائج الأقرب لوصفك",
          en: "Closest results to your description",
          ur: "آپ کی تفصیل کے قریب ترین نتائج",
        });

  const resultDescription = onlyLostReports
    ? tr({
        ar: "هذه البلاغات لأغراض أبلغ أصحابها عن فقدانها، وقد يكون أحدها مطابقًا للغرض الذي عثرت عليه.",
        en: "These items were reported lost by their owners. One may match the item you found.",
        ur: "ان اشیا کے مالکان نے انہیں گمشدہ رپورٹ کیا ہے۔ ان میں سے کوئی چیز آپ کو ملی ہوئی چیز سے مل سکتی ہے۔",
      })
    : onlyFoundReports
      ? tr({
          ar: "هذه البلاغات لأغراض أُبلغ عن العثور عليها، وقد يكون أحدها مطابقًا للغرض الذي فقدته.",
          en: "These items were reported found. One may match the item you lost.",
          ur: "ان اشیا کے ملنے کی رپورٹ دی گئی ہے۔ ان میں سے کوئی چیز آپ کی گمشدہ چیز سے مل سکتی ہے۔",
        })
      : tr({
          ar: "راجع كل بلاغ بهدوء، ثم افتح التفاصيل إذا بدا أنه الغرض الذي تبحث عنه.",
          en: "Review each report, then open the details if it looks like the item you're looking for.",
          ur: "ہر رپورٹ دیکھیں، اور اگر چیز درست لگے تو تفصیلات کھولیں۔",
        });

  const resultEyebrow = onlyLostReports
    ? tr({ ar: "نبحث في بلاغات المفقودات", en: "Searching lost reports", ur: "گمشدہ رپورٹس میں تلاش" })
    : onlyFoundReports
      ? tr({ ar: "نبحث في بلاغات العثور", en: "Searching found reports", ur: "ملنے والی رپورٹس میں تلاش" })
      : tr({ ar: "نتائج البحث", en: "Search results", ur: "تلاش کے نتائج" });

  return (
    <div className="mt-7 border-t border-border/60 pt-7 animate-fade-up">
      <div className="mb-6 flex flex-col gap-4 sm:flex-row sm:items-end sm:justify-between">
        <div className="max-w-2xl">
          <div className="inline-flex items-center gap-2 text-[11px] font-bold tracking-[0.08em] text-primary">
            <span className="size-1.5 rounded-full bg-primary" />
            {resultEyebrow}
          </div>

          <h2 className="mt-2 font-display text-xl font-bold tracking-tight text-foreground sm:text-2xl">
            {resultTitle}
          </h2>

          <p className="mt-2 max-w-xl text-sm leading-7 text-muted-foreground">
            {resultDescription}
          </p>
        </div>

        <div className="inline-flex w-fit items-center gap-2 rounded-full border border-border bg-stone-50 px-3 py-1.5 text-xs text-muted-foreground">
          <span className="font-bold tabular-nums text-foreground">{results.length}</span>
          <span>{tr({ ar: "بلاغ", en: "reports", ur: "رپورٹس" })}</span>
        </div>
      </div>

      {(ownReportsExcluded > 0 || dismissedExcluded > 0) && (
        <div className="mb-5 rounded-2xl border border-border/70 bg-stone-50/70 px-4 py-3 text-xs leading-relaxed text-muted-foreground">
          {ownReportsExcluded > 0 && <p>{t("searchOwnExcludedNote")}</p>}
          {dismissedExcluded > 0 && (
            <p className={ownReportsExcluded > 0 ? "mt-1" : ""}>
              {t("searchDismissedExcludedNote")}
            </p>
          )}
        </div>
      )}

      {topMatch && (
        <TopMatchCard
          report={topMatch}
          smartSearchState={smartSearchState}
          t={t}
          tr={tr}
        />
      )}

      {secondary.length > 0 && (
        <div className="mt-7">
          <div className="mb-3 flex items-center justify-between gap-3 px-1">
            <p className="text-xs font-semibold text-foreground/75">
              {tr({ ar: "بلاغات أخرى قريبة", en: "Other close reports", ur: "دیگر قریبی رپورٹس" })}
            </p>
            <span className="text-[11px] tabular-nums text-muted-foreground">
              {secondary.length}
            </span>
          </div>

          <div className="grid gap-3 md:grid-cols-2">
            {secondary.map((report, index) => (
              <SecondaryMatchCard
                key={report.reportId}
                report={report}
                smartSearchState={smartSearchState}
                t={t}
                tr={tr}
                delayMs={100 + index * 40}
              />
            ))}
          </div>
        </div>
      )}
    </div>
  );
}

function ItemThumb({ report, size = "size-14" }) {
  if (report.imagePath) {
    return (
      <img
        src={reportImageUrl(report.imagePath)}
        alt=""
        className={`${size} shrink-0 rounded-2xl border border-border/60 object-cover`}
      />
    );
  }

  return (
    <div
      className={`${size} grid shrink-0 place-items-center rounded-2xl border border-border/70 bg-stone-50 text-primary/40`}
    >
      <Sparkles className="size-4.5" strokeWidth={1.5} />
    </div>
  );
}

function TopMatchCard({ report, smartSearchState, t, tr }) {
  return (
    <Link
      to={`/match/${report.reportId}?source=smart-search`}
      state={{
        scorePercentage: report.scorePercentage,
        smartSearchState,
      }}
      className="group relative block overflow-hidden rounded-[1.75rem] border border-primary/20 bg-card p-5 shadow-soft transition-all duration-200 hover:-translate-y-0.5 hover:border-primary/30 hover:shadow-md sm:p-6"
    >
      <span className="absolute inset-y-0 start-0 w-1 bg-primary" />

      <div className="flex items-center justify-between gap-4 ps-1">
        <div className="inline-flex items-center gap-2 text-[11px] font-bold text-primary">
          <Sparkles className="size-3.5" strokeWidth={1.7} />
          {tr({ ar: "أقرب نتيجة", en: "Closest result", ur: "قریب ترین نتیجہ" })}
        </div>

        {typeof report.scorePercentage === "number" && (
          <span className="rounded-full bg-primary/[0.07] px-3 py-1.5 text-xs font-extrabold tabular-nums text-primary">
            {Math.round(report.scorePercentage)}%
          </span>
        )}
      </div>

      <div className="mt-5 flex items-start gap-4 ps-1">
        {report.imagePath && <ItemThumb report={report} size="size-16 sm:size-20" />}

        <div className="min-w-0 flex-1">
          <h3 className="font-display text-xl font-bold leading-8 text-foreground transition-colors group-hover:text-primary sm:text-2xl">
            {reportHeadingTitle(report, t("browseTitle"))}
          </h3>

          <div className="mt-3 flex flex-wrap items-center gap-x-4 gap-y-2 text-xs text-muted-foreground">
            {report.location && (
              <span className="inline-flex items-center gap-1.5">
                <MapPin className="size-3.5" />
                {report.location}
              </span>
            )}

            {report.date && (
              <span className="inline-flex items-center gap-1.5">
                <Calendar className="size-3.5" />
                {report.date}
              </span>
            )}

            {report.color && (
              <span className="rounded-full bg-stone-100 px-2.5 py-1 text-[11px] font-medium text-foreground/65">
                {report.color}
              </span>
            )}
          </div>
        </div>
      </div>

      <div className="mt-6 flex items-center justify-between border-t border-border/60 pt-4 ps-1">
        <span className="text-xs leading-relaxed text-muted-foreground">
          {tr({ ar: "افتح البلاغ لمراجعة التفاصيل", en: "Open the report to review details", ur: "تفصیلات دیکھنے کے لیے رپورٹ کھولیں" })}
        </span>
        <span className="inline-flex shrink-0 items-center gap-1.5 text-sm font-semibold text-primary">
          {trViewLabel(t)}
          <ArrowUpRight className="size-4 transition-transform group-hover:translate-x-0.5 rtl:group-hover:-translate-x-0.5" />
        </span>
      </div>
    </Link>
  );
}

function trViewLabel(t) {
  return t("searchViewReport");
}

function SecondaryMatchCard({ report, smartSearchState, t, tr, delayMs }) {
  const hasScore = typeof report.scorePercentage === "number";

  return (
    <Link
      to={`/match/${report.reportId}?source=smart-search`}
      state={{
        scorePercentage: report.scorePercentage,
        smartSearchState,
      }}
      className="group flex min-h-[112px] items-start gap-3.5 rounded-2xl border border-border bg-card p-4 transition-all duration-200 animate-fade-up hover:-translate-y-0.5 hover:border-primary/25 hover:shadow-soft"
      style={{ animationDelay: `${delayMs}ms` }}
    >
      <ItemThumb report={report} size="size-12" />

      <div className="min-w-0 flex-1">
        <div className="flex items-start justify-between gap-3">
          <h4 className="line-clamp-2 text-sm font-semibold leading-6 text-foreground transition-colors group-hover:text-primary">
            {reportHeadingTitle(report, t("browseTitle"))}
          </h4>

          {hasScore && (
            <span className="shrink-0 rounded-full bg-primary/[0.055] px-2.5 py-1 text-[11px] font-bold tabular-nums text-primary">
              {Math.round(report.scorePercentage)}%
            </span>
          )}
        </div>

        <div className="mt-2 flex flex-wrap items-center gap-x-3 gap-y-1 text-[11px] text-muted-foreground">
          {report.location && (
            <span className="inline-flex min-w-0 items-center gap-1.5">
              <MapPin className="size-3 shrink-0" />
              <span className="max-w-[180px] truncate">{report.location}</span>
            </span>
          )}

          {report.color && (
            <span className="rounded-full bg-stone-100 px-2 py-0.5 text-[10px] font-medium text-foreground/60">
              {report.color}
            </span>
          )}
        </div>

        <div className="mt-3 inline-flex items-center gap-1 text-[11px] font-semibold text-primary opacity-75 transition-opacity group-hover:opacity-100">
          {tr({ ar: "عرض البلاغ", en: "View report", ur: "رپورٹ دیکھیں" })}
          <ArrowUpRight className="size-3.5" />
        </div>
      </div>
    </Link>
  );
}
