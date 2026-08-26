import { useEffect, useRef, useState } from "react";
import { Link } from "react-router-dom";
import { Search, Loader2, AlertCircle, MapPin, Sparkles, RotateCcw, X, ImagePlus } from "lucide-react";

import { useI18n } from "../lib/useI18n";
import { useAuth } from "../lib/useAuth";
import DammaMark from "../components/DammaMark";
import { aiSearch, imageFileToBase64 } from "../api/search";
import { reportImageUrl } from "../api/reports";
import { listMatches, getMyDismissedReportIds } from "../api/matches";
import { ReportType, MatchStatus } from "../api/enums";
import { fetchMyReports } from "../lib/myReports";
import { validateImageFile, ImageValidationReason } from "../lib/imageValidation";
import { reportHeadingTitle } from "../lib/reportTitle";

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

// Phase 4 Part 7 (Task C): defaulting to "all" — a first-time visitor to
// /search should see results across both Lost and Found by default,
// without needing to manually change the filter first. (Previously
// defaulted to "found" on the theory that "I lost something, help me
// find who has it" was the dominant case — overridden by this task's
// explicit instruction; "Found"/"Lost" remain one click away either way.)
const TYPE_FILTERS = [
  { key: "found", value: ReportType.FOUND },
  { key: "lost", value: ReportType.LOST },
  { key: "all", value: undefined },
];

export default function SmartSearch() {
  const { t, tr } = useI18n();
  const { userId } = useAuth();
  const [text, setText] = useState("");
  const [typeFilter, setTypeFilter] = useState("all");
  const [status, setStatus] = useState("idle"); // idle | loading | success | empty | error
  const [results, setResults] = useState([]);
  const [errorMsg, setErrorMsg] = useState(null);

  // Conversational search (Task: preserve context across messages) - context
  // is the previous turn's extracted {type, description, color, location} —
  // a single concise current value each, echoed back on the next search so
  // ai_service can combine it with the new message/image; it is never the
  // full conversation text. history is the visible chat log (user turns +
  // assistant replies/follow-up prompts) rendered inside the interaction
  // box. Both live only in this component's state - no persistence, no
  // server session, cleared on navigation away from the page.
  const [context, setContext] = useState(null);
  const [history, setHistory] = useState([]);

  const [ownReportsExcluded, setOwnReportsExcluded] = useState(0);
  // Phase 4 Part 4: mirrors ownReportsExcluded for the dismissed-pair
  // filter (Phase 4 Part 3) — see runSearch and the "empty" state render
  // below for why this needs its own counter, not just a shared one.
  const [dismissedExcluded, setDismissedExcluded] = useState(0);

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
  const [lastSearchWasImageOnly, setLastSearchWasImageOnly] = useState(false);

  // Task C — Smart Image Search: image is entirely additive to the existing
  // text search, not a separate feature/endpoint (see the request contract
  // note below).
  const [imageFile, setImageFile] = useState(null);
  const [imagePreview, setImagePreview] = useState(null);
  const [imageError, setImageError] = useState(null);
  const [isDragOver, setIsDragOver] = useState(false);
  const fileInputRef = useRef(null);

  useEffect(() => {
    document.title = tr({ ar: "البحث الذكي — لُقيا", en: "Smart search — Luqya", ur: "ذہین تلاش — لقیا" });
  }, [tr]);

  useEffect(() => {
    return () => {
      if (imagePreview) URL.revokeObjectURL(imagePreview);
    };
  }, [imagePreview]);

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
    setText("");
    handleImageFile(null);

    setStatus("loading");
    setErrorMsg(null);
    setOwnReportsExcluded(0);
    setDismissedExcluded(0);
    setLastSearchWasImageOnly(Boolean(imageFile) && !text.trim());

    try {
      // Verified request contract (AiSearchInputDto): Text and ImageBase64
      // are independently optional, both go to the SAME POST
      // api/app/ai-search/search endpoint / AiMatchingService scoring path
      // that text-only search already used — no new/parallel endpoint (Task
      // E2). Only send `type` when a specific one is selected —
      // AiSearchInputDto.Type is nullable on the backend specifically so
      // omitting it searches both.
      const selected = TYPE_FILTERS.find((f) => f.key === typeFilter);
      const imageBase64 = imageFile ? await imageFileToBase64(imageFile) : undefined;
      const data = await aiSearch({
        text: text.trim() || undefined,
        imageBase64,
        type: selected?.value,
        maxResults: 12,
        context,
      });

      // The assistant's turn in the chat log — reply covers the greeting/
      // incomplete/complete-search cases, followUpPrompt covers the image-
      // search "here are results, want to add a location?" case. The two
      // are mutually exclusive today, but either one (never both being
      // needed at once) must reach the log so a real reply is never hidden
      // just because results happened to come back empty.
      const assistantText = data.reply || data.followUpPrompt || null;
      if (assistantText) {
        setHistory((prev) => [...prev, { role: "assistant", text: assistantText }]);
      }

      // Only overwrite context when this turn actually extracted something —
      // a bare greeting extracts nothing and must not wipe out an item
      // already described in an earlier turn.
      if (data.extractedType || data.extractedDescription || data.extractedColor || data.extractedLocation) {
        setContext({
          type: data.extractedType || null,
          description: data.extractedDescription || null,
          color: data.extractedColor || null,
          location: data.extractedLocation || null,
        });
      }

      let filtered = data.results ?? [];

      // Exclude the current user's own reports from recovery candidates —
      // only when ownership can be verified from real data. AiSearchResultDto
      // has no ownership field of its own, so this cross-references against
      // the current user's own reports (resolved via Report.CreatorId in
      // fetchMyReports) — not a cached, session-only guess.
      if (userId) {
        let dismissedCount = 0;

        const mine = await fetchMyReports({ userId });
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
        // resurfacing for them).
        try {
          const dismissedIds = await getMyDismissedReportIds();
          if (dismissedIds?.length > 0) {
            const dismissedSet = new Set(dismissedIds);
            const beforeDirect = filtered.length;
            filtered = filtered.filter((r) => !dismissedSet.has(r.reportId));
            dismissedCount += beforeDirect - filtered.length;
          }
        } catch {
          // Non-fatal, same reasoning as the Match-based exclusion above.
        }

        setDismissedExcluded(dismissedCount);
      }

      if (!data.shouldMatch) {
        // Reply-only turn (greeting or incomplete description) — no search
        // ran, so this must never render as "no matching reports."
        setResults([]);
        setStatus("reply");
      } else if (filtered.length > 0) {
        setResults(filtered);
        setStatus("success");
      } else {
        setResults([]);
        setStatus("empty");
      }
    } catch (err) {
      setStatus("error");
      setErrorMsg(err.message || t("searchErrorGeneric"));
    }
  }

  return (
    <section className="py-16 lg:py-24">
      <div className="max-w-4xl mx-auto px-6">
        <div className="text-center mb-10">
          <div className="inline-flex items-center gap-2 text-[11px] font-mono uppercase tracking-widest text-primary font-bold mb-3">
            <DammaMark className="size-3.5" />
            {t("navSearch")}
          </div>
          <h1 className="font-display text-4xl lg:text-5xl font-extrabold tracking-tight mb-3">
            {t("searchTitle")}
          </h1>
          <p className="text-muted-foreground text-lg max-w-xl mx-auto">{t("searchSub")}</p>
        </div>

        <form onSubmit={runSearch} className="bg-card border border-border rounded-[2rem] p-6 lg:p-8 shadow-soft">
          {/* Task A: conversation history rendered inside the existing
              interaction box, above the input — user/assistant turns only,
              client-side, cleared on navigation. Reuses the same colors
              already used elsewhere on this page (primary for the button/
              score badge, stone-100 for the filter pill track) — no new
              design language. */}
          {history.length > 0 && (
            <div className="mb-4 space-y-3 max-h-80 overflow-y-auto">
              {history.map((entry, index) => (
                <div key={index} className={`flex ${entry.role === "user" ? "justify-end" : "justify-start"}`}>
                  <div
                    className={`max-w-[85%] rounded-2xl px-4 py-2.5 text-sm ${
                      entry.role === "user"
                        ? "bg-primary text-primary-foreground"
                        : "bg-stone-100 text-foreground"
                    }`}
                  >
                    {entry.text}
                  </div>
                </div>
              ))}
            </div>
          )}

          <div className="relative">
            <textarea
              value={text}
              onChange={(e) => setText(e.target.value)}
              placeholder={t("searchPh")}
              rows={3}
              className="w-full px-5 py-4 rounded-2xl bg-stone-50 border border-stone-200 focus:border-primary focus:outline-none focus:ring-2 focus:ring-primary/10 transition-all resize-none text-base"
            />
          </div>

          {/* Task C: image attach — drag-drop or click, preview, remove/replace.
              Fully optional and additive to the text box above (image only,
              text only, or both — see runSearch's guard). */}
          <div className="mt-4">
            {imagePreview ? (
              <div className="relative rounded-2xl overflow-hidden border border-stone-200 max-w-xs">
                <img src={imagePreview} alt="" className="w-full h-40 object-cover" />
                <div className="absolute inset-x-0 bottom-0 flex items-center justify-between gap-2 bg-black/50 px-3 py-2">
                  <button
                    type="button"
                    onClick={() => fileInputRef.current?.click()}
                    className="text-xs font-semibold text-white hover:underline"
                  >
                    {t("searchImageReplaceCta")}
                  </button>
                  <button
                    type="button"
                    onClick={() => handleImageFile(null)}
                    className="inline-flex items-center gap-1 text-xs font-semibold text-white hover:underline"
                  >
                    <X className="size-3.5" />
                    {t("searchImageRemoveCta")}
                  </button>
                </div>
              </div>
            ) : (
              <label
                onDragOver={(e) => {
                  e.preventDefault();
                  setIsDragOver(true);
                }}
                onDragLeave={() => setIsDragOver(false)}
                onDrop={handleDrop}
                className={`flex items-center gap-3 rounded-2xl border-2 border-dashed px-5 py-4 cursor-pointer transition-colors ${
                  isDragOver ? "border-primary bg-primary/[0.03]" : "border-stone-200 hover:border-primary/40 hover:bg-primary/[0.02]"
                }`}
              >
                <div className="size-9 rounded-xl bg-primary/5 text-primary grid place-items-center shrink-0">
                  <ImagePlus className="size-4" strokeWidth={1.5} />
                </div>
                <div>
                  <p className="text-sm font-semibold">{t("searchByImageLabel")}</p>
                  <p className="text-xs text-muted-foreground">{t("searchByImageHint")}</p>
                </div>
                <input
                  ref={fileInputRef}
                  type="file"
                  accept="image/*"
                  onChange={(e) => handleImageFile(e.target.files?.[0] ?? null)}
                  className="hidden"
                />
              </label>
            )}
            {imageError && <p className="text-xs text-error mt-2">{imageError}</p>}
          </div>

          <div className="flex flex-wrap items-center justify-between gap-4 mt-5">
            <div className="inline-flex p-1.5 bg-stone-100 rounded-full gap-1">
              {TYPE_FILTERS.map(({ key }) => (
                <button
                  key={key}
                  type="button"
                  onClick={() => setTypeFilter(key)}
                  className={`px-4 py-2 rounded-full text-xs font-semibold transition-all ${
                    typeFilter === key ? "bg-primary text-primary-foreground shadow-soft" : "text-foreground/60 hover:text-foreground"
                  }`}
                >
                  {key === "all" ? t("searchTypeAll") : t(key)}
                </button>
              ))}
            </div>

            <button
              type="submit"
              disabled={status === "loading" || (!text.trim() && !imageFile)}
              className="inline-flex items-center gap-2 bg-primary text-primary-foreground px-6 py-3 rounded-2xl font-semibold shadow-glow hover:-translate-y-0.5 transition-transform disabled:opacity-60 disabled:translate-y-0"
            >
              {status === "loading" ? <Loader2 className="size-4 animate-spin" /> : <Search className="size-4" />}
              {status === "loading"
                ? imageFile
                  ? t("searchingByImageLabel")
                  : t("searchLoading")
                : tr({ ar: "إرسال", en: "Send", ur: "بھیجیں" })}
            </button>
          </div>

          <p className="mt-3 text-xs text-muted-foreground">
            {typeFilter === "found" ? t("searchFoundHint") : typeFilter === "lost" ? t("searchLostHint") : null}
          </p>
        </form>

        <div className="mt-10">
          {status === "error" && (
            <div className="flex flex-col items-center gap-3 py-16 text-center">
              <AlertCircle className="size-6 text-error" />
              <p className="text-error text-sm">{errorMsg}</p>
              <button
                type="button"
                onClick={runSearch}
                className="inline-flex items-center gap-2 text-sm font-semibold text-primary hover:underline"
              >
                <RotateCcw className="size-3.5" />
                {t("searchRetry")}
              </button>
            </div>
          )}

          {status === "empty" && (
            <div className="text-center py-16 text-muted-foreground">
              {/* Phase 4 Part 4: a real match can exist and still leave
                  filtered.length === 0 once the own-reports/dismissed-pair
                  exclusions (Phase 4 Part 3 and earlier) run — that is NOT
                  the same situation as "the AI genuinely found nothing",
                  and showing the same generic message for both was the
                  confirmed root cause of a real reported discrepancy
                  (search-page "no results" vs. a real, existing backend
                  match) — see Phase-4-Part-4 report. Do not merge this back
                  into a single unconditional message. */}
              {ownReportsExcluded > 0 || dismissedExcluded > 0 ? (
                <>
                  <p>{t("searchAllExcludedNote")}</p>
                  {ownReportsExcluded > 0 && (
                    <p className="mt-2 text-sm">{t("searchOwnExcludedNote")}</p>
                  )}
                  {dismissedExcluded > 0 && (
                    <p className="mt-2 text-sm">{t("searchDismissedExcludedNote")}</p>
                  )}
                </>
              ) : (
                <p>{t("searchEmpty")}</p>
              )}
              {lastSearchWasImageOnly && (
                <p className="mt-2 text-sm">{t("searchEmptyImageOnlyHint")}</p>
              )}
            </div>
          )}

          {status === "success" && (
            <>
              {ownReportsExcluded > 0 && (
                <p className="text-xs text-muted-foreground mb-4">{t("searchOwnExcludedNote")}</p>
              )}
              {dismissedExcluded > 0 && (
                <p className="text-xs text-muted-foreground mb-4">{t("searchDismissedExcludedNote")}</p>
              )}
              <div className="grid sm:grid-cols-2 lg:grid-cols-3 gap-5 animate-rise-in">
                {results.map((r) => (
                  <div
                    key={r.reportId}
                    className="group rounded-[1.75rem] border border-border bg-card overflow-hidden shadow-soft hover:shadow-luxe transition-all"
                  >
                    <Link
                      to={`/match/${r.reportId}`}
                      state={{ scorePercentage: r.scorePercentage }}
                      className="block hover:-translate-y-1 transition-transform"
                    >
                      <div className="aspect-[16/10] bg-gradient-to-br from-stone-100 to-stone-200 relative grid place-items-center overflow-hidden">
                        {r.imagePath ? (
                          <img
                            src={reportImageUrl(r.imagePath)}
                            alt=""
                            className="absolute inset-0 size-full object-cover"
                          />
                        ) : (
                          <Sparkles className="size-8 text-primary/40" />
                        )}
                        {typeof r.scorePercentage === "number" && (
                          <span className="absolute top-3 end-3 bg-primary text-primary-foreground text-xs font-bold font-mono px-2.5 py-1 rounded-full">
                            {Math.round(r.scorePercentage)}%
                          </span>
                        )}
                      </div>
                      <div className="p-5">
                        <h3 className="font-bold mb-2 group-hover:text-primary transition-colors line-clamp-2">
                          {reportHeadingTitle(r, t("browseTitle"))}
                        </h3>
                        {r.color && (
                          <div className="flex items-center gap-1.5 text-xs text-muted-foreground">
                            <MapPin className="size-3" />
                            {r.color}
                          </div>
                        )}
                      </div>
                    </Link>
                  </div>
                ))}
              </div>
            </>
          )}
        </div>
      </div>
    </section>
  );
}
