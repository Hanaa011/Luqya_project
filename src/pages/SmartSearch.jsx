import { useEffect, useRef, useState } from "react";
import { Link, useNavigate } from "react-router-dom";
import { Search, Loader2, AlertCircle, MapPin, Sparkles, RotateCcw, X, ImagePlus, Check, UserX } from "lucide-react";

import { useI18n } from "../lib/useI18n";
import { useAuth } from "../lib/useAuth";
import DammaMark from "../components/DammaMark";
import { aiSearch, imageFileToBase64 } from "../api/search";
import { reportImageUrl } from "../api/reports";
import { claimMatch, listMatches } from "../api/matches";
import { ReportType, ReportStatus, MatchStatus } from "../api/enums";
import { fetchMyReports } from "../lib/myReports";
import { validateImageFile, ImageValidationReason } from "../lib/imageValidation";

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

// Defaulting to "found": the dominant use case here is "I lost something,
// help me find who has it" — so the default search scope is the other
// side of that exchange. "Lost"/"All" stay one click away for the
// opposite case (you found something and want to see if it was reported
// missing).
const TYPE_FILTERS = [
  { key: "found", value: ReportType.FOUND },
  { key: "lost", value: ReportType.LOST },
  { key: "all", value: undefined },
];

export default function SmartSearch() {
  const { t, tr } = useI18n();
  const navigate = useNavigate();
  const { profile, userId } = useAuth();
  const [text, setText] = useState("");
  const [typeFilter, setTypeFilter] = useState("found");
  const [status, setStatus] = useState("idle"); // idle | loading | success | empty | error
  const [results, setResults] = useState([]);
  const [errorMsg, setErrorMsg] = useState(null);
  const [ownReportsExcluded, setOwnReportsExcluded] = useState(0);
  // Phase 4 Part 4: mirrors ownReportsExcluded for the dismissed-pair
  // filter (Phase 4 Part 3) — see runSearch and the "empty" state render
  // below for why this needs its own counter, not just a shared one.
  const [dismissedExcluded, setDismissedExcluded] = useState(0);

  // Phase 4 Part 3: "This is my item" / "Not my item" claim flow state for
  // whichever single result card currently has it open. Shape:
  // { reportId, resultType, observedScorePercentage, action: "mine"|"not-mine",
  //   status: "loading"|"no-eligible"|"picking"|"submitting"|"success"|"error",
  //   eligible?: Report[], selectedReportId?: string, error?: string }
  const [claim, setClaim] = useState(null);

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

    setStatus("loading");
    setErrorMsg(null);
    setOwnReportsExcluded(0);
    setDismissedExcluded(0);
    setLastSearchWasImageOnly(Boolean(imageFile) && !text.trim());
    setClaim(null);

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
      });

      let filtered = data ?? [];

      // Exclude the current user's own reports from recovery candidates —
      // only when ownership can be verified from real data. AiSearchResultDto
      // has no ownership field of its own, so this cross-references against
      // the current user's own reports (resolved via Report.CreatorId in
      // fetchMyReports) — not a cached, session-only guess.
      if (userId) {
        const mine = await fetchMyReports({ userId });
        if (mine.reliable && mine.reports.length > 0) {
          const myIds = new Set(mine.reports.map((r) => r.id));
          const before = filtered.length;
          filtered = filtered.filter((r) => !myIds.has(r.reportId));
          setOwnReportsExcluded(before - filtered.length);

          // Phase 4 Part 3 (Task A.4/decision #3): "Not my item" is
          // persisted as a real, permanent MatchStatus.Rejected row
          // (api/matches.js claimMatch) — this is where that persistence
          // actually takes effect on a future search: any candidate the
          // user already dismissed against ANY of their own reports is
          // excluded again here, the same real-data cross-reference
          // pattern as the own-reports exclusion just above, not a
          // session-local list. Real exclusion, traced through the actual
          // candidate list returned by this search, not just recorded and
          // ignored.
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
              setDismissedExcluded(beforeDismissed - filtered.length);
            }
          } catch {
            // Non-fatal: if the matches list can't be fetched, dismissed
            // candidates just aren't re-filtered for this one search — the
            // own-reports exclusion above (independent of this call)
            // still applies.
          }
        }
      }

      if (filtered.length > 0) {
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

  // Phase 4 Part 3 (Task B) — entry point for both "This is my item" and
  // "Not my item", started directly from a result card on this page
  // (decision #4: the claim action lives on /search, not only on a
  // specific report's own page).
  async function startClaim(result, action) {
    if (!profile) {
      navigate("/auth/login");
      return;
    }

    setClaim({ reportId: result.reportId, resultType: result.type, observedScorePercentage: result.scorePercentage, action, status: "loading" });

    const mine = await fetchMyReports({ userId });
    if (!mine.reliable) {
      setClaim({
        reportId: result.reportId,
        action,
        status: "error",
        error: tr({
          ar: "تعذّر تحميل بلاغاتك. حاول مرة أخرى.",
          en: "Couldn't load your reports. Please try again.",
          ur: "آپ کی رپورٹس لوڈ نہیں ہو سکیں۔ دوبارہ کوشش کریں۔",
        }),
      });
      return;
    }

    // Task B.3: opposite type (a Found result can only be claimed against
    // a Lost report of the user's own, and vice versa) and "still open/
    // unresolved" — Closed reports are done, not a fit for a new claim.
    const eligible = mine.reports.filter(
      (r) => r.type !== result.type && r.status !== ReportStatus.CLOSED
    );

    if (eligible.length === 0) {
      setClaim({ reportId: result.reportId, action, status: "no-eligible" });
      return;
    }

    if (eligible.length === 1) {
      await confirmClaim(result, action, eligible[0].id);
      return;
    }

    setClaim({
      reportId: result.reportId,
      action,
      status: "picking",
      eligible,
      selectedReportId: eligible[0].id,
    });
  }

  async function confirmClaim(result, action, ownReportId) {
    setClaim((current) => ({ ...(current ?? {}), reportId: result.reportId, action, status: "submitting" }));

    try {
      await claimMatch({
        searchResultReportId: result.reportId,
        ownReportId,
        observedScorePercentage: result.scorePercentage,
        isMine: action === "mine",
      });

      if (action === "mine") {
        setClaim({ reportId: result.reportId, action, status: "success" });
        // Decision #2: Contact is immediately reachable — no waiting on
        // the other party. A short pause here is purely so the success
        // state is visible before navigating away, not a gate of any kind.
        window.setTimeout(() => navigate(`/match/${result.reportId}/contact`), 900);
      } else {
        // Task B.6: low-friction — the dismissed result simply leaves the
        // current view immediately, no further confirmation needed.
        setResults((prev) => prev.filter((r) => r.reportId !== result.reportId));
        setClaim(null);
      }
    } catch (err) {
      setClaim({
        reportId: result.reportId,
        action,
        status: "error",
        error:
          err.message ||
          tr({
            ar: "تعذّر تنفيذ الإجراء. حاول مرة أخرى.",
            en: "Couldn't complete that action. Please try again.",
            ur: "یہ عمل مکمل نہیں ہو سکا۔ دوبارہ کوشش کریں۔",
          }),
      });
    }
  }

  function cancelClaim() {
    setClaim(null);
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
                : t("searchBtn")}
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
                    <Link to={`/match/${r.reportId}`} className="block hover:-translate-y-1 transition-transform">
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
                      <div className="p-5 pb-0">
                        <h3 className="font-bold mb-2 group-hover:text-primary transition-colors line-clamp-2">
                          {r.description || r.aiObjectType}
                        </h3>
                        {r.color && (
                          <div className="flex items-center gap-1.5 text-xs text-muted-foreground">
                            <MapPin className="size-3" />
                            {r.color}
                          </div>
                        )}
                      </div>
                    </Link>

                    {/* Phase 4 Part 3: claim entry point, directly on the
                        search result card (decision #4) — deliberately
                        outside the Link above (stopPropagation isn't
                        needed since these buttons aren't nested inside an
                        anchor at all). */}
                    <div className="p-5 pt-3 flex gap-2">
                      <button
                        type="button"
                        onClick={() => startClaim(r, "mine")}
                        disabled={claim?.reportId === r.reportId && claim.status !== "error" && claim.status !== "no-eligible" && claim.status !== "picking"}
                        className="flex-1 inline-flex items-center justify-center gap-1.5 rounded-xl bg-success-tint text-success text-xs font-bold px-3 py-2.5 hover:opacity-80 transition-opacity disabled:opacity-50"
                      >
                        <Check className="size-3.5" />
                        {t("claimMineCta")}
                      </button>
                      <button
                        type="button"
                        onClick={() => startClaim(r, "not-mine")}
                        disabled={claim?.reportId === r.reportId && claim.status !== "error" && claim.status !== "no-eligible" && claim.status !== "picking"}
                        className="flex-1 inline-flex items-center justify-center gap-1.5 rounded-xl border border-border text-muted-foreground text-xs font-bold px-3 py-2.5 hover:bg-stone-100 transition-colors disabled:opacity-50"
                      >
                        <UserX className="size-3.5" />
                        {t("claimNotMineCta")}
                      </button>
                    </div>

                    {claim?.reportId === r.reportId && (
                      <ClaimPanel
                        claim={claim}
                        tr={tr}
                        onSelectReport={(id) => setClaim((c) => ({ ...c, selectedReportId: id }))}
                        onConfirm={() => confirmClaim(r, claim.action, claim.selectedReportId)}
                        onCancel={cancelClaim}
                      />
                    )}
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

// Phase 4 Part 3 (Task B.3/B.4): the inline panel shown under a result
// card while a claim is in progress — covers every state startClaim/
// confirmClaim can produce: loading the user's own reports, no eligible
// report to claim against, picking one when there's more than one, an
// in-flight submit, a brief success state (before navigating to Contact),
// and a retryable error.
function ClaimPanel({ claim, tr, onSelectReport, onConfirm, onCancel }) {
  const isMine = claim.action === "mine";

  if (claim.status === "loading") {
    return (
      <div className="px-5 pb-5 flex items-center gap-2 text-xs text-muted-foreground">
        <Loader2 className="size-3.5 animate-spin" />
        {tr({ ar: "جارٍ التحقق من بلاغاتك…", en: "Checking your reports…", ur: "آپ کی رپورٹس چیک ہو رہی ہیں…" })}
      </div>
    );
  }

  if (claim.status === "no-eligible") {
    return (
      <div className="px-5 pb-5">
        <p className="text-xs text-muted-foreground leading-relaxed">
          {tr({
            ar: "لا يوجد لديك بلاغ مفتوح من النوع المقابل لربط هذا الإجراء به.",
            en: "You don't have an open report of the matching type to link this to.",
            ur: "اس عمل کو منسلک کرنے کے لیے آپ کے پاس مناسب قسم کی کوئی کھلی رپورٹ نہیں ہے۔",
          })}{" "}
          <Link to="/report" className="font-semibold text-primary hover:underline">
            {tr({ ar: "أنشئ بلاغًا", en: "Create one", ur: "ایک بنائیں" })}
          </Link>
        </p>
        <button type="button" onClick={onCancel} className="mt-2 text-xs font-semibold text-muted-foreground hover:text-foreground">
          {tr({ ar: "إغلاق", en: "Dismiss", ur: "برخاست کریں" })}
        </button>
      </div>
    );
  }

  if (claim.status === "picking") {
    return (
      <div className="px-5 pb-5 border-t border-border pt-4 mt-1">
        <p className="text-xs font-semibold mb-2">
          {tr({
            ar: "أي من بلاغاتك يتعلق بهذا؟",
            en: "Which of your reports is this?",
            ur: "یہ آپ کی کون سی رپورٹ سے متعلق ہے؟",
          })}
        </p>
        <div className="space-y-1.5 mb-3">
          {claim.eligible.map((report) => (
            <label key={report.id} className="flex items-center gap-2 text-xs cursor-pointer">
              <input
                type="radio"
                name={`claim-report-${claim.reportId}`}
                checked={claim.selectedReportId === report.id}
                onChange={() => onSelectReport(report.id)}
              />
              <span className="truncate">{report.description || report.aiObjectType || report.id}</span>
            </label>
          ))}
        </div>
        <div className="flex gap-2">
          <button
            type="button"
            onClick={onConfirm}
            className="flex-1 rounded-xl bg-primary text-primary-foreground text-xs font-bold py-2"
          >
            {tr({ ar: "تأكيد", en: "Confirm", ur: "تصدیق کریں" })}
          </button>
          <button
            type="button"
            onClick={onCancel}
            className="rounded-xl border border-border text-xs font-semibold px-3 py-2"
          >
            {tr({ ar: "إلغاء", en: "Cancel", ur: "منسوخ کریں" })}
          </button>
        </div>
      </div>
    );
  }

  if (claim.status === "submitting") {
    return (
      <div className="px-5 pb-5 flex items-center gap-2 text-xs text-muted-foreground">
        <Loader2 className="size-3.5 animate-spin" />
        {tr({ ar: "جارٍ التنفيذ…", en: "Submitting…", ur: "جمع ہو رہا ہے…" })}
      </div>
    );
  }

  if (claim.status === "success" && isMine) {
    return (
      <div className="px-5 pb-5 flex items-center gap-2 text-xs font-semibold text-success">
        <Check className="size-3.5" />
        {tr({
          ar: "تم! جارٍ نقلك إلى صفحة التواصل…",
          en: "Confirmed! Taking you to the contact page…",
          ur: "تصدیق ہو گئی! رابطہ صفحہ کی طرف لے جایا جا رہا ہے…",
        })}
      </div>
    );
  }

  if (claim.status === "error") {
    return (
      <div className="px-5 pb-5">
        <p className="text-xs text-error mb-1.5">{claim.error}</p>
        <button type="button" onClick={onCancel} className="text-xs font-semibold text-muted-foreground hover:text-foreground">
          {tr({ ar: "إغلاق", en: "Dismiss", ur: "برخاست کریں" })}
        </button>
      </div>
    );
  }

  return null;
}
