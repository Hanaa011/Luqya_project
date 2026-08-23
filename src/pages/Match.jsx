import { useEffect, useMemo, useRef, useState } from "react";
import { Link, useLocation, useNavigate, useParams, useSearchParams } from "react-router-dom";
import {
  AlertCircle,
  ArrowRight,
  CalendarDays,
  Check,
  CheckCircle2,
  Clock3,
  ExternalLink,
  Loader2,
  MapPin,
  MessageCircle,
  PackageCheck,
  SearchCheck,
  Sparkles,
  Tag,
  Trash2,
  UserX,
  XCircle,
} from "lucide-react";

import { useI18n } from "../lib/useI18n";
import { useAuth } from "../lib/useAuth";
import { deleteReport, getReport, reportImageUrl, updateReport } from "../api/reports";
import { acceptMatch, claimMatch, listMatches, rejectMatch } from "../api/matches";
import { fetchMyReports } from "../lib/myReports";
import { reportHeadingTitle } from "../lib/reportTitle";
import {
  MatchStatus,
  ReportStatus,
  ReportType,
  matchStatusLabelKey,
  reportStatusLabelKey,
} from "../api/enums";
import { ApiError } from "../api/httpClient";

function copy(lang, values) {
  return values[lang] ?? values.en;
}


function getMatchScore(match) {
  const value = Number(match?.similarityScore);
  return Number.isFinite(value) ? Math.round(value) : null;
}

// Phase 4 Part 6 (Task C): heading now prefers a short, extracted
// portion of the description (reportHeadingTitle) over the generic
// AI-classified object type - aiObjectType is only the final fallback,
// used when the description is empty/whitespace.
function reportHeading(report, fallback) {
  return {
    title: reportHeadingTitle(report, fallback),
    description: report?.description || null,
  };
}

function updatePayload(report, overrides = {}) {
  return {
    status: report.status,
    description: report.description ?? null,
    locationDetails: report.locationDetails ?? null,
    lostFoundDate: report.lostFoundDate ?? null,
    imagePath: report.imagePath ?? null,
    isItemWithFinder: Boolean(report.isItemWithFinder),
    pickupLocation: report.pickupLocation ?? null,
    ...overrides,
  };
}

function sameDay(a, b) {
  if (!a || !b) return false;
  return new Date(a).toISOString().slice(0, 10) === new Date(b).toISOString().slice(0, 10);
}

function normalize(value) {
  return String(value ?? "").trim().toLocaleLowerCase();
}

function sharedAttributes(a, b) {
  if (!a || !b) return [];
  const left = [a.aiObjectType, a.aiBrand, a.color, ...(a.aiTags ?? [])].filter(Boolean);
  const right = new Set([b.aiObjectType, b.aiBrand, b.color, ...(b.aiTags ?? [])].filter(Boolean).map(normalize));
  return [...new Set(left.filter((item) => right.has(normalize(item))))];
}

export default function Match() {
  const { id } = useParams();
  const navigate = useNavigate();
  const location = useLocation();
  const [searchParams] = useSearchParams();
  const fromReportId = searchParams.get("from");
  const { t, tr, lang } = useI18n();
  const { profile, userId } = useAuth();

  const [report, setReport] = useState(null);
  const [pairedReport, setPairedReport] = useState(null);
  const [match, setMatch] = useState(null);
  const [matchCandidates, setMatchCandidates] = useState([]);
  const [showMatchCandidates, setShowMatchCandidates] = useState(false);
  const [myReportIds, setMyReportIds] = useState(new Set());
  const [loading, setLoading] = useState(true);
  const [errorCode, setErrorCode] = useState(null);
  const [workingAction, setWorkingAction] = useState(null);
  const [actionError, setActionError] = useState(null);
  const matchCandidatesRef = useRef(null);

  // Phase 4 Part 5 (Task B): the claim action, relocated here from
  // SmartSearch.jsx's result cards. The score is carried forward via
  // router state from wherever this page was linked from with a real,
  // observed AI score (SmartSearch.jsx, ReportLost.jsx's own immediate
  // candidates) - never recomputed or fabricated here, per Phase 4 Part
  // 3's decision #1 that a claimed match's SimilarityScore is stored
  // verbatim from what the user actually saw. If this page was reached
  // without that context (e.g. a direct link, Browse, Dashboard), there
  // is no honest score to attach a new claim to, so the claim action is
  // simply not offered in that case - see the Phase 4 Part 5 report.
  const navScore = location.state?.scorePercentage;
  const claimableScore = typeof navScore === "number" && Number.isFinite(navScore) ? navScore : null;
  const [claim, setClaim] = useState(null);

  useEffect(() => {
    document.title = "Report details — Luqya";
  }, []);

  useEffect(() => {
    if (!showMatchCandidates) return undefined;

    const frame = window.requestAnimationFrame(() => {
      matchCandidatesRef.current?.scrollIntoView({
        behavior: "smooth",
        block: "start",
      });
    });

    return () => window.cancelAnimationFrame(frame);
  }, [showMatchCandidates]);

  useEffect(() => {
    let cancelled = false;
    const controller = new AbortController();

    Promise.resolve().then(() => {
      if (cancelled) return;
      setLoading(true);
      setErrorCode(null);
      setMatchCandidates([]);
      setShowMatchCandidates(false);
      setClaim(null);
    });

    const ownershipPromise = userId
      ? fetchMyReports({ userId, maxResultCount: 500, signal: controller.signal })
      : Promise.resolve({ reliable: false, reports: [] });

    Promise.all([
      getReport(id, controller.signal),
      listMatches({ maxResultCount: 200, sorting: "creationTime desc" }, controller.signal).catch(() => ({ items: [] })),
      ownershipPromise,
    ])
      .then(async ([reportData, matchesData, mineData]) => {
        if (cancelled) return;

        const ids = new Set(mineData?.reliable ? mineData.reports.map((item) => item.id) : []);
        const relatedMatches = (matchesData?.items ?? []).filter(
          (item) => item.lostReportId === reportData.id || item.foundReportId === reportData.id
        );
        const requestedMatch = fromReportId
          ? relatedMatches.find((item) => {
              const otherId = item.lostReportId === reportData.id ? item.foundReportId : item.lostReportId;
              return otherId === fromReportId;
            })
          : null;
        const primaryMatch =
          requestedMatch ??
          relatedMatches.find((item) => item.status === MatchStatus.PENDING) ??
          relatedMatches[0] ??
          null;

        const candidateMatches = relatedMatches
          .filter((item) => {
            const otherId =
              item.lostReportId === reportData.id
                ? item.foundReportId
                : item.lostReportId;
            return !ids.has(otherId);
          })
          .sort((a, b) => (getMatchScore(b) ?? 0) - (getMatchScore(a) ?? 0));

        setReport(reportData);
        setMyReportIds(ids);
        setMatch(primaryMatch);

        const loadedCandidates = await Promise.all(
          candidateMatches.map(async (candidateMatch) => {
            const otherId =
              candidateMatch.lostReportId === reportData.id
                ? candidateMatch.foundReportId
                : candidateMatch.lostReportId;
            try {
              const candidateReport = await getReport(otherId, controller.signal);
              return { match: candidateMatch, report: candidateReport };
            } catch {
              return null;
            }
          })
        );

        if (!cancelled) {
          setMatchCandidates(loadedCandidates.filter(Boolean));
        }

        if (primaryMatch) {
          const otherId = primaryMatch.lostReportId === reportData.id ? primaryMatch.foundReportId : primaryMatch.lostReportId;
          const alreadyLoaded = loadedCandidates.find((candidate) => candidate?.report?.id === otherId)?.report;
          if (alreadyLoaded) {
            if (!cancelled) setPairedReport(alreadyLoaded);
          } else {
            try {
              const other = await getReport(otherId, controller.signal);
              if (!cancelled) setPairedReport(other);
            } catch {
              if (!cancelled) setPairedReport(null);
            }
          }
        } else {
          setPairedReport(null);
        }
      })
      .catch((error) => {
        if (cancelled) return;
        setErrorCode(error instanceof ApiError && error.status === 404 ? "not-found" : "generic");
      })
      .finally(() => !cancelled && setLoading(false));

    return () => {
      cancelled = true;
      controller.abort();
    };
  }, [fromReportId, id, userId]);

  const isOwnedReport = report ? myReportIds.has(report.id) : false;
  const ownsPairedReport = pairedReport ? myReportIds.has(pairedReport.id) : false;
  const isReviewingMatch =
    Boolean(match && pairedReport) && !isOwnedReport && ownsPairedReport;
  const isLost = report?.type === ReportType.LOST;
  const details = useMemo(() => reportHeading(report, t("browseTitle")), [report, t]);
  const score = getMatchScore(match);

  const evidence = useMemo(() => {
    if (!report || !pairedReport) return [];
    const signals = [];
    const shared = sharedAttributes(report, pairedReport);

    if (shared.length > 0) {
      signals.push({
        Icon: Tag,
        title: copy(lang, { ar: "سمات متشابهة", en: "Matching item attributes", ur: "ملتی جلتی خصوصیات" }),
        detail: shared.slice(0, 4).join(" · "),
      });
    }
    if (report.categoryId && pairedReport.categoryId && report.categoryId === pairedReport.categoryId) {
      signals.push({
        Icon: SearchCheck,
        title: copy(lang, { ar: "نفس الفئة", en: "Same category", ur: "ایک ہی زمرہ" }),
        detail: copy(lang, { ar: "كلا البلاغين مصنفان ضمن الفئة نفسها.", en: "Both reports share the same category.", ur: "دونوں رپورٹس ایک ہی زمرے میں ہیں۔" }),
      });
    }
    if (report.locationId && pairedReport.locationId && report.locationId === pairedReport.locationId) {
      signals.push({
        Icon: MapPin,
        title: copy(lang, { ar: "نفس الموقع", en: "Same location", ur: "ایک ہی مقام" }),
        detail: report.locationDetails || pairedReport.locationDetails || copy(lang, { ar: "الموقع نفسه في بيانات البلاغين.", en: "The reports reference the same location.", ur: "دونوں رپورٹس ایک ہی مقام کا حوالہ دیتی ہیں۔" }),
      });
    }
    if (sameDay(report.lostFoundDate, pairedReport.lostFoundDate)) {
      signals.push({
        Icon: CalendarDays,
        title: copy(lang, { ar: "نفس التاريخ", en: "Same reported date", ur: "ایک ہی تاریخ" }),
        detail: new Date(report.lostFoundDate).toLocaleDateString(lang),
      });
    }
    return signals;
  }, [lang, pairedReport, report]);

  async function handleMatchDecision(kind) {
    if (!match || workingAction) return;
    setActionError(null);
    setWorkingAction(kind);
    try {
      const updated = kind === "accept" ? await acceptMatch(match.id) : await rejectMatch(match.id);
      setMatch(updated);
    } catch {
      setActionError(copy(lang, { ar: "تعذّر تحديث المطابقة. حاول مرة أخرى.", en: "Couldn't update the match. Please try again.", ur: "میچ اپ ڈیٹ نہیں ہو سکا۔ دوبارہ کوشش کریں۔" }));
    } finally {
      setWorkingAction(null);
    }
  }

  async function handleClose() {
    if (!report || workingAction) return;
    setActionError(null);
    setWorkingAction("close");
    try {
      const updated = await updateReport(report.id, updatePayload(report, { status: ReportStatus.CLOSED }));
      setReport(updated);
    } catch {
      setActionError(copy(lang, { ar: "تعذّر إغلاق البلاغ. حاول مرة أخرى.", en: "Couldn't close the report. Please try again.", ur: "رپورٹ بند نہیں ہو سکی۔ دوبارہ کوشش کریں۔" }));
    } finally {
      setWorkingAction(null);
    }
  }

  async function handleDelete() {
    if (!report || workingAction) return;
    const confirmed = window.confirm(
      copy(lang, {
        ar: "هل أنت متأكد من حذف هذا البلاغ؟ لا يمكن التراجع عن هذا الإجراء.",
        en: "Delete this report? This action can't be undone.",
        ur: "کیا آپ واقعی یہ رپورٹ حذف کرنا چاہتے ہیں؟ یہ عمل واپس نہیں ہو سکتا۔",
      })
    );
    if (!confirmed) return;

    setActionError(null);
    setWorkingAction("delete");
    try {
      await deleteReport(report.id);
      navigate("/dashboard");
    } catch {
      setActionError(copy(lang, { ar: "تعذّر حذف البلاغ. حاول مرة أخرى.", en: "Couldn't delete the report. Please try again.", ur: "رپورٹ حذف نہیں ہو سکی۔ دوبارہ کوشش کریں۔" }));
    } finally {
      setWorkingAction(null);
    }
  }

  // Phase 4 Part 5 (Task B): relocated, not duplicated, from
  // SmartSearch.jsx's own startClaim/confirmClaim (Phase 4 Part 3) - same
  // auth redirect, same fetchMyReports-based eligible-report resolution
  // and picker, same claimMatch call, same immediate navigation to
  // Contact on success.
  async function startClaim(action) {
    if (!profile) {
      // Carry this exact detail page (including its carried-forward AI
      // score, in location.state) through the login round trip, so a
      // successful login returns the user here with the claim action
      // still available - see Login.jsx/RequireAuth.jsx.
      navigate("/auth/login", {
        state: { from: location.pathname, fromState: location.state },
      });
      return;
    }

    // Phase 4 Part 8 (Task B): "not my item" is now a simple confirmation
    // only - no picker, no eligible-report lookup, no dependency on
    // whether the caller owns any report at all. This is the entire
    // redesign: dismissing a result never had a real reason to involve
    // any of the caller's own reports in the first place.
    if (action === "not-mine") {
      setClaim({ action, status: "confirming" });
      return;
    }

    setClaim({ action, status: "loading" });

    const mine = await fetchMyReports({ userId });
    if (!mine.reliable) {
      setClaim({
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

    const eligible = mine.reports.filter(
      (r) => r.type !== report.type && r.status !== ReportStatus.CLOSED
    );

    if (eligible.length === 0) {
      // Phase 4 Part 6 (Task B): confirming "this is my item" must not be
      // blocked by requiring a report the user doesn't have - proceed
      // directly with no OwnReportId; the backend grants contact access
      // via a narrower, single-report claim instead of a full Match (see
      // ClaimResultDto).
      await confirmClaim(action, null);
      return;
    }

    // Phase 4 Part 7 (Task A): the picker is now always shown whenever at
    // least one eligible report exists - even exactly one - so the user
    // always gets the chance to say none of their existing reports
    // actually relate to this item (the "none of these" option, added
    // below the real reports in ClaimPanel's "picking" state). The old
    // "exactly one eligible report -> auto-select, skip the picker"
    // shortcut (Phase 4 Part 5) is removed per this task's explicit design.
    // (Only "this is my item" reaches this point at all - "not my item"
    // returned above, per Phase 4 Part 8's redesign.)
    setClaim({
      action,
      status: "picking",
      eligible,
      selectedReportId: eligible[0].id,
    });
  }

  async function confirmClaim(action, ownReportId) {
    setClaim((current) => ({ ...(current ?? {}), action, status: "submitting" }));

    try {
      const result = await claimMatch({
        searchResultReportId: report.id,
        ownReportId,
        observedScorePercentage: claimableScore,
        isMine: action === "mine",
      });

      if (action === "mine") {
        // Phase 4 Part 6 (Task B): result.match is null when this claim
        // used the no-own-report path - an honest, distinct success note
        // for that case, since it genuinely won't show up as a linked
        // match in either party's Dashboard (there's no second report to
        // pair it with), unlike the full-Match path above it.
        setClaim({ action, status: "success", noOwnReport: !result?.match });
        // Decision #2 (Phase 4 Part 3): Contact is immediately reachable -
        // the pause is purely so the success state is visible, not a gate.
        window.setTimeout(() => navigate(`/match/${report.id}/contact`), 900);
      } else {
        // Task B.2.5: unlike the old inline card (which just removed
        // itself from the results list), there's no list to remove this
        // page's own report from - take the user back to a sensible
        // place instead of leaving them on the report they just dismissed.
        setClaim({ action, status: "success" });
        window.setTimeout(() => navigate("/search"), 900);
      }
    } catch (err) {
      setClaim({
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

  if (loading) {
    return (
      <div className="flex items-center justify-center gap-2 py-32 text-muted-foreground">
        <Loader2 className="size-5 animate-spin" />
        {copy(lang, { ar: "جارٍ تحميل البلاغ...", en: "Loading report...", ur: "رپورٹ لوڈ ہو رہی ہے..." })}
      </div>
    );
  }

  if (errorCode || !report) {
    return (
      <div className="flex flex-col items-center justify-center gap-3 py-32 px-6 text-center">
        <AlertCircle className="size-7 text-error" />
        <p className="text-sm text-muted-foreground">
          {errorCode === "not-found"
            ? copy(lang, { ar: "لم يُعثر على هذا البلاغ.", en: "This report couldn't be found.", ur: "یہ رپورٹ نہیں مل سکی۔" })
            : copy(lang, { ar: "تعذّر تحميل تفاصيل البلاغ.", en: "Couldn't load the report details.", ur: "رپورٹ کی تفصیلات لوڈ نہیں ہو سکیں۔" })}
        </p>
        <Link to="/browse" className="mt-2 text-sm font-semibold text-primary hover:underline">
          {copy(lang, { ar: "العودة إلى البلاغات", en: "Back to reports", ur: "رپورٹس پر واپس جائیں" })}
        </Link>
      </div>
    );
  }

  const typeTone = isLost
    ? "border-warn/20 bg-warn-tint/60 text-warn"
    : "border-success/20 bg-success-tint/60 text-success";
  const intentText = isLost
    ? copy(lang, {
        ar: "هذا بلاغ عن غرض مفقود. الهدف الآن هو استعادته وربطه بمن عثر عليه.",
        en: "This is a lost-item report. The next goal is recovery and connecting it with whoever found it.",
        ur: "یہ گمشدہ چیز کی رپورٹ ہے۔ اگلا مقصد اسے واپس پانا اور ملنے والے شخص سے جوڑنا ہے۔",
      })
    : copy(lang, {
        ar: "هذا بلاغ عن غرض تم العثور عليه. الهدف الآن هو إعادته إلى صاحبه بأمان.",
        en: "This is a found-item report. The next goal is returning it safely to its owner.",
        ur: "یہ ملی ہوئی چیز کی رپورٹ ہے۔ اگلا مقصد اسے محفوظ طریقے سے مالک تک پہنچانا ہے۔",
      });

  return (
    <section className="py-8 sm:py-10 lg:py-12">
      <div className="mx-auto max-w-7xl px-4 sm:px-6">
        <div className={`mb-6 rounded-[1.75rem] border px-5 py-5 sm:px-7 ${typeTone}`}>
          <div className="flex flex-wrap items-start justify-between gap-4">
            <div className="max-w-3xl">
              <div className="mb-2 flex flex-wrap items-center gap-2">
                <span className="rounded-full bg-current/10 px-3 py-1 text-xs font-extrabold">
                  {isLost ? t("lost") : t("found")}
                </span>
                <span className="rounded-full border border-current/[0.15] px-3 py-1 text-xs font-semibold">
                  {t(reportStatusLabelKey(report.status))}
                </span>
              </div>
              <p className="text-sm font-medium leading-relaxed text-foreground/75">{intentText}</p>
            </div>
            {isLost ? <SearchCheck className="size-6" /> : <PackageCheck className="size-6" />}
          </div>
        </div>

        <div className="grid gap-6 lg:grid-cols-[minmax(0,1.45fr)_minmax(310px,0.75fr)] lg:items-start">
          <div className="space-y-6">
            <article className="overflow-hidden rounded-[2rem] border border-border bg-card shadow-soft">
              {report.imagePath && (
                <div className="aspect-[16/8] overflow-hidden bg-stone-100 sm:aspect-[16/7]">
                  <img src={reportImageUrl(report.imagePath)} alt="" className="size-full object-cover" />
                </div>
              )}

              <div className="p-6 sm:p-8 lg:p-10">
                <h1 className="font-display text-3xl font-extrabold tracking-tight sm:text-4xl lg:text-5xl">
                  {details.title}
                </h1>
                {details.description && (
                  <p className="mt-4 max-w-3xl text-base leading-8 text-muted-foreground sm:text-lg">
                    {details.description}
                  </p>
                )}

                <div className="mt-7 grid gap-3 sm:grid-cols-2 lg:grid-cols-3">
                  <MetaItem Icon={MapPin} label={copy(lang, { ar: "الموقع", en: "Location", ur: "مقام" })} value={report.locationDetails || "—"} />
                  <MetaItem
                    Icon={CalendarDays}
                    label={copy(lang, { ar: "تاريخ الفقد / العثور", en: "Lost / found date", ur: "گم / ملنے کی تاریخ" })}
                    value={report.lostFoundDate ? new Date(report.lostFoundDate).toLocaleDateString(lang) : "—"}
                  />
                  <MetaItem
                    Icon={Clock3}
                    label={copy(lang, { ar: "تاريخ إنشاء البلاغ", en: "Report created", ur: "رپورٹ بنائی گئی" })}
                    value={new Date(report.creationTime).toLocaleDateString(lang)}
                  />
                </div>

                {(report.aiObjectType || report.aiBrand || report.color || report.aiTags?.length > 0) && (
                  <div className="mt-8 border-t border-border pt-7">
                    <p className="mb-3 text-[11px] font-mono uppercase tracking-widest text-muted-foreground">
                      {copy(lang, { ar: "تصنيف الغرض", en: "Item classification", ur: "چیز کی درجہ بندی" })}
                    </p>
                    <div className="flex flex-wrap gap-2">
                      {[report.aiObjectType, report.aiBrand, report.color, ...(report.aiTags ?? [])]
                        .filter(Boolean)
                        .map((item) => (
                          <span key={item} className="rounded-full bg-stone-100 px-3 py-1.5 text-xs font-medium text-foreground/75">
                            {item}
                          </span>
                        ))}
                    </div>
                  </div>
                )}
              </div>
            </article>

            {isOwnedReport && showMatchCandidates && (
              <section
                ref={matchCandidatesRef}
                id="match-candidates"
                className="scroll-mt-24 rounded-[2rem] border border-border bg-card p-5 sm:p-7"
              >
                <div className="flex flex-wrap items-end justify-between gap-3 border-b border-border pb-5">
                  <div>
                    <p className="text-[10px] font-bold tracking-[0.14em] text-primary">
                      {copy(lang, { ar: "مطابقات البلاغ", en: "Report matches", ur: "رپورٹ کے میچز" })}
                    </p>
                    <h2 className="mt-1 font-display text-xl font-bold sm:text-2xl">
                      {copy(lang, { ar: "اختر البلاغ الذي تريد مراجعته", en: "Choose a report to review", ur: "جس رپورٹ کا جائزہ لینا ہے اسے منتخب کریں" })}
                    </h2>
                    <p className="mt-1 text-sm leading-relaxed text-muted-foreground">
                      {copy(lang, {
                        ar: "نعرض جميع المطابقات المرتبطة بهذا البلاغ، مرتبة من أعلى نسبة إلى الأقل.",
                        en: "Showing all matches linked to this report, ordered from highest score to lowest.",
                        ur: "اس رپورٹ سے منسلک تمام میچز، زیادہ اسکور سے کم اسکور تک۔",
                      })}
                    </p>
                  </div>
                  <span className="rounded-full bg-primary/[0.07] px-3 py-1 text-xs font-bold text-primary">
                    {matchCandidates.length}
                  </span>
                </div>

                {matchCandidates.length > 0 ? (
                  <div className="divide-y divide-border">
                    {matchCandidates.map(({ match: candidateMatch, report: candidateReport }) => {
                      const candidateDetails = reportHeading(candidateReport, t("browseTitle"));
                      const candidateScore = getMatchScore(candidateMatch);

                      return (
                        <Link
                          key={candidateMatch.id}
                          to={`/match/${candidateReport.id}?from=${report.id}`}
                          className="group flex items-center gap-4 py-5 transition-colors first:pt-5 last:pb-1 hover:text-primary focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
                        >
                          <div className="grid size-14 shrink-0 place-items-center rounded-2xl bg-primary/[0.07] text-primary">
                            <span className="font-display text-base font-extrabold">{candidateScore}%</span>
                          </div>

                          <div className="min-w-0 flex-1">
                            <div className="flex flex-wrap items-center gap-2">
                              <h3 className="truncate font-display text-base font-bold text-foreground sm:text-lg">
                                {candidateDetails.title}
                              </h3>
                              <span className="rounded-full bg-stone-100 px-2.5 py-1 text-[10px] font-semibold text-muted-foreground">
                                {candidateReport.type === ReportType.LOST ? t("lost") : t("found")}
                              </span>
                            </div>
                            <div className="mt-2 flex flex-wrap gap-x-4 gap-y-1.5 text-xs text-muted-foreground">
                              <span className="inline-flex items-center gap-1.5">
                                <MapPin className="size-3.5" />
                                {candidateReport.locationDetails || "—"}
                              </span>
                              <span className="inline-flex items-center gap-1.5">
                                <CalendarDays className="size-3.5" />
                                {candidateReport.lostFoundDate
                                  ? new Date(candidateReport.lostFoundDate).toLocaleDateString(lang)
                                  : "—"}
                              </span>
                            </div>
                          </div>

                          <span className="shrink-0 text-xs font-semibold text-primary opacity-0 transition-opacity group-hover:opacity-100 sm:text-sm">
                            {copy(lang, { ar: "عرض التفاصيل", en: "View details", ur: "تفصیلات دیکھیں" })}
                          </span>
                        </Link>
                      );
                    })}
                  </div>
                ) : (
                  <p className="py-6 text-sm text-muted-foreground">
                    {copy(lang, {
                      ar: "لا توجد حاليًا مطابقات مرتبطة بهذا البلاغ.",
                      en: "There are currently no matches linked to this report.",
                      ur: "اس رپورٹ سے فی الحال کوئی میچ منسلک نہیں ہے۔",
                    })}
                  </p>
                )}
              </section>
            )}

            {isReviewingMatch && (
            <section className="rounded-[2rem] border border-border bg-card p-6 sm:p-8">
              <div className="flex flex-wrap items-start justify-between gap-4">
                <div>
                  <div className="mb-2 flex items-center gap-2 text-primary">
                    <Sparkles className="size-4" />
                    <span className="text-[11px] font-mono uppercase tracking-widest font-bold">
                      {copy(lang, { ar: "تفسير المطابقة", en: "Match explanation", ur: "میچ کی وضاحت" })}
                    </span>
                  </div>
                  <h2 className="font-display text-xl font-bold sm:text-2xl">
                    {match
                      ? copy(lang, { ar: "لماذا اقترح الذكاء الاصطناعي هذه المطابقة؟", en: "Why did AI suggest this match?", ur: "اے آئی نے یہ میچ کیوں تجویز کیا؟" })
                      : copy(lang, { ar: "لا توجد مطابقة مقترحة بعد", en: "No suggested match yet", ur: "ابھی کوئی تجویز کردہ میچ نہیں" })}
                  </h2>
                </div>

                {score != null && (
                  <div className="min-w-28 rounded-2xl bg-primary px-4 py-3 text-primary-foreground">
                    <p className="text-[10px] uppercase tracking-widest opacity-70">
                      {copy(lang, { ar: "الثقة", en: "Confidence", ur: "اعتماد" })}
                    </p>
                    <p className="mt-1 font-display text-3xl font-extrabold">{score}%</p>
                  </div>
                )}
              </div>

              {score != null && (
                <div className="mt-6 h-2 overflow-hidden rounded-full bg-stone-100" aria-label={`${score}%`}>
                  <div className="h-full rounded-full bg-primary transition-[width] duration-700" style={{ width: `${Math.min(100, Math.max(0, score))}%` }} />
                </div>
              )}

              {!match ? (
                <p className="mt-5 text-sm leading-relaxed text-muted-foreground">
                  {copy(lang, {
                    ar: "لم تُرجع واجهة المطابقات مطابقة مرتبطة بهذا البلاغ ضمن النتائج الحالية.",
                    en: "The matches API didn't return a match linked to this report in the current result window.",
                    ur: "موجودہ نتائج میں matches API نے اس رپورٹ سے متعلق کوئی میچ واپس نہیں کیا۔",
                  })}
                </p>
              ) : match.matchReason ? (
                <p className="mt-5 rounded-2xl bg-primary/[0.035] px-4 py-3 text-sm leading-relaxed text-foreground/75">
                  {match.matchReason}
                </p>
              ) : (
                <p className="mt-5 text-sm leading-relaxed text-muted-foreground">
                  {copy(lang, {
                    ar: "واجهة البرمجة لم تُرجع سببًا نصيًا مفصلًا لهذه المطابقة.",
                    en: "The API didn't return a detailed text explanation for this match.",
                    ur: "API نے اس میچ کے لیے تفصیلی متنی وجہ واپس نہیں کی۔",
                  })}
                </p>
              )}

              {evidence.length > 0 ? (
                <div className="mt-6 grid gap-3 sm:grid-cols-2">
                  {evidence.map(({ Icon, title, detail }) => (
                    <div key={title} className="flex items-start gap-3 rounded-2xl border border-border p-4">
                      <span className="mt-0.5 grid size-7 shrink-0 place-items-center rounded-full bg-success-tint text-success">
                        <Check className="size-3.5" />
                      </span>
                      <div className="min-w-0">
                        <div className="flex items-center gap-1.5 font-semibold">
                          <Icon className="size-3.5 text-primary" />
                          <span>{title}</span>
                        </div>
                        <p className="mt-1 text-xs leading-relaxed text-muted-foreground">{detail}</p>
                      </div>
                    </div>
                  ))}
                </div>
              ) : match ? (
                <p className="mt-5 text-xs leading-relaxed text-muted-foreground">
                  {copy(lang, {
                    ar: "يعرض لُقيا فقط إشارات يمكن التحقق منها من بيانات البلاغين؛ لا نعرض أسبابًا مختلقة غير موجودة في الاستجابة.",
                    en: "Luqya only shows signals that can be verified from the two API responses; it doesn't invent missing explanation fields.",
                    ur: "لُقیا صرف وہ اشارے دکھاتا ہے جو دونوں API جوابات سے تصدیق ہو سکیں؛ گمشدہ وضاحتیں گھڑی نہیں جاتیں۔",
                  })}
                </p>
              ) : null}

              {pairedReport && (
                <Link
                  to={`/match/${pairedReport.id}`}
                  className="mt-6 inline-flex min-h-11 items-center gap-2 rounded-xl text-sm font-semibold text-primary hover:underline focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
                >
                  {copy(lang, { ar: "فتح البلاغ الآخر في المطابقة", en: "Open the other report in this match", ur: "اس میچ کی دوسری رپورٹ کھولیں" })}
                  <ExternalLink className="size-4" />
                </Link>
              )}
            </section>
            )}
          </div>

          <aside className="space-y-5 lg:sticky lg:top-24">
            <section className="rounded-[2rem] border border-border bg-card p-6 shadow-soft">
              <p className="text-[11px] font-mono uppercase tracking-widest text-muted-foreground">
                {copy(lang, { ar: "الخطوة التالية", en: "What can I do now?", ur: "اب میں کیا کر سکتا ہوں؟" })}
              </p>
              <h2 className="mt-2 font-display text-xl font-bold">
                {isOwnedReport
                  ? copy(lang, { ar: "إدارة بلاغك", en: "Manage your report", ur: "اپنی رپورٹ سنبھالیں" })
                  : copy(lang, { ar: "اتخذ الإجراء المناسب", en: "Choose the next action", ur: "اگلا مناسب قدم منتخب کریں" })}
              </h2>

              <div className="mt-5 space-y-2.5">
                {actionError && (
                  <div className="flex items-start gap-2 rounded-xl bg-error-tint px-3 py-2.5 text-xs leading-relaxed text-error">
                    <AlertCircle className="mt-0.5 size-3.5 shrink-0" />
                    <span>{actionError}</span>
                  </div>
                )}

                {isOwnedReport ? (
                  <>

                    {matchCandidates.length > 0 && (
                      <button
                        type="button"
                        onClick={() => setShowMatchCandidates((current) => !current)}
                        aria-expanded={showMatchCandidates}
                        aria-controls="match-candidates"
                        className="flex min-h-12 w-full cursor-pointer items-center justify-between gap-3 rounded-2xl border border-border px-4 text-sm font-semibold transition-all duration-200 hover:-translate-y-0.5 hover:border-primary/40 hover:bg-primary/[0.05] hover:text-primary hover:shadow-sm focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary/20"
                      >
                        <span className="inline-flex items-center gap-3">
                          <Sparkles className="size-4" />
                          {showMatchCandidates
                            ? copy(lang, { ar: "إخفاء المطابقات", en: "Hide matches", ur: "میچز چھپائیں" })
                            : copy(lang, { ar: "مراجعة المطابقات", en: "Review matches", ur: "میچز کا جائزہ لیں" })}
                        </span>
                        <span className="rounded-full bg-primary/[0.07] px-2.5 py-1 text-xs font-extrabold text-primary">
                          {matchCandidates.length}
                        </span>
                      </button>
                    )}

                    {report.status !== ReportStatus.CLOSED && (
                      <button
                        type="button"
                        onClick={handleClose}
                        disabled={workingAction === "close"}
                        className="flex min-h-12 w-full cursor-pointer items-center gap-3 rounded-2xl border border-border px-4 text-sm font-semibold transition-all duration-200 hover:-translate-y-0.5 hover:border-success/40 hover:bg-success-tint/60 hover:text-success hover:shadow-sm focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-success/20 disabled:pointer-events-none disabled:opacity-50"
                      >
                        {workingAction === "close" ? <Loader2 className="size-4 animate-spin" /> : <CheckCircle2 className="size-4 text-success" />}
                        {report.status === ReportStatus.MATCHED
                          ? isLost
                            ? copy(lang, { ar: "تم استرداد الغرض", en: "Mark as recovered", ur: "واپس ملنے کے طور پر نشان زد کریں" })
                            : copy(lang, { ar: "تمت إعادة الغرض", en: "Mark as returned", ur: "واپس کیے جانے کے طور پر نشان زد کریں" })
                          : copy(lang, { ar: "إغلاق البلاغ", en: "Close report", ur: "رپورٹ بند کریں" })}
                      </button>
                    )}

                    <button
                      type="button"
                      onClick={handleDelete}
                      disabled={workingAction === "delete"}
                      className="flex min-h-12 w-full cursor-pointer items-center gap-3 rounded-2xl px-4 text-sm font-semibold text-error transition-all duration-200 hover:-translate-y-0.5 hover:bg-error-tint hover:shadow-sm focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-error/20 disabled:pointer-events-none disabled:opacity-50"
                    >
                      {workingAction === "delete" ? <Loader2 className="size-4 animate-spin" /> : <Trash2 className="size-4" />}
                      {copy(lang, { ar: "حذف البلاغ", en: "Delete report", ur: "رپورٹ حذف کریں" })}
                    </button>
                  </>
                ) : (
                  <>
                    {report.status === ReportStatus.CLOSED ? (
                      <div className="rounded-2xl bg-stone-100 px-4 py-3 text-sm leading-relaxed text-muted-foreground">
                        {copy(lang, {
                          ar: "هذا البلاغ مغلق ولا يحتاج إجراءً إضافيًا.",
                          en: "This report is closed and doesn't need another action.",
                          ur: "یہ رپورٹ بند ہے اور مزید کارروائی کی ضرورت نہیں۔",
                        })}
                      </div>
                    ) : profile && isReviewingMatch ? (
                      <Link
                        to={`/match/${report.id}/contact`}
                        className="flex min-h-12 w-full cursor-pointer items-center justify-between gap-3 rounded-2xl bg-primary px-4 text-sm font-semibold text-primary-foreground shadow-sm transition-all duration-200 hover:-translate-y-0.5 hover:bg-primary/90 hover:shadow-md focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary/25"
                      >
                        <span className="inline-flex items-center gap-3">
                          <MessageCircle className="size-4" />
                          {isLost
                            ? copy(lang, { ar: "التواصل مع صاحب الغرض", en: "Contact the owner", ur: "مالک سے رابطہ کریں" })
                            : copy(lang, { ar: "التواصل مع من عثر عليه", en: "Contact the finder", ur: "ملنے والے سے رابطہ کریں" })}
                        </span>
                        <ArrowRight className={`size-4 ${lang === "ar" || lang === "ur" ? "rotate-180" : ""}`} />
                      </Link>
                    ) : !isReviewingMatch && claimableScore != null ? (
                      <ClaimAction
                        t={t}
                        tr={tr}
                        lang={lang}
                        claim={claim}
                        onStart={startClaim}
                        onSelectReport={(reportId) => setClaim((c) => ({ ...c, selectedReportId: reportId }))}
                        onConfirm={() => confirmClaim(claim.action, claim.selectedReportId)}
                        onCancel={cancelClaim}
                      />
                    ) : !profile ? (
                      <Link to="/auth/login" className="flex min-h-12 w-full cursor-pointer items-center justify-center rounded-2xl bg-primary px-4 text-sm font-semibold text-primary-foreground transition-all duration-200 hover:-translate-y-0.5 hover:bg-primary/90 hover:shadow-md focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary/25">
                        {copy(lang, { ar: "سجّل الدخول للتواصل", en: "Log in to contact", ur: "رابطے کے لیے لاگ ان کریں" })}
                      </Link>
                    ) : null}

                    {report.status !== ReportStatus.CLOSED && ownsPairedReport && match?.status === MatchStatus.PENDING && (
                      <div className="mt-4 border-t border-border pt-4">
                        <p className="mb-3 text-xs leading-relaxed text-muted-foreground">
                          {copy(lang, {
                            ar: "هذه المطابقة مرتبطة بأحد بلاغاتك. أكدها فقط إذا راجعت التفاصيل وتعرّفت على الغرض.",
                            en: "This match involves one of your reports. Confirm it only after reviewing the details.",
                            ur: "یہ میچ آپ کی ایک رپورٹ سے متعلق ہے۔ تفصیلات دیکھنے کے بعد ہی تصدیق کریں۔",
                          })}
                        </p>
                        <div className="grid grid-cols-2 gap-2">
                          <button
                            type="button"
                            onClick={() => handleMatchDecision("accept")}
                            disabled={Boolean(workingAction)}
                            className="inline-flex min-h-11 cursor-pointer items-center justify-center gap-2 rounded-xl bg-success px-3 text-xs font-bold text-white transition-all duration-200 hover:-translate-y-0.5 hover:opacity-90 hover:shadow-sm disabled:pointer-events-none disabled:opacity-50"
                          >
                            {workingAction === "accept" ? <Loader2 className="size-4 animate-spin" /> : <Check className="size-4" />}
                            {copy(lang, { ar: "تأكيد المطابقة", en: "Confirm match", ur: "میچ کی تصدیق" })}
                          </button>
                          <button
                            type="button"
                            onClick={() => handleMatchDecision("reject")}
                            disabled={Boolean(workingAction)}
                            className="inline-flex min-h-11 cursor-pointer items-center justify-center gap-2 rounded-xl border border-border px-3 text-xs font-bold text-muted-foreground transition-all duration-200 hover:-translate-y-0.5 hover:border-foreground/20 hover:bg-stone-100 hover:text-foreground hover:shadow-sm disabled:pointer-events-none disabled:opacity-50"
                          >
                            <XCircle className="size-4" />
                            {copy(lang, { ar: "ليست مطابقة", en: "Not a match", ur: "یہ میچ نہیں" })}
                          </button>
                        </div>
                      </div>
                    )}
                  </>
                )}
              </div>
            </section>

            {match && (
              <section className="rounded-[1.75rem] border border-border bg-card p-5">
                <div className="flex items-center justify-between gap-3">
                  <div>
                    <p className="text-[11px] font-mono uppercase tracking-widest text-muted-foreground">
                      {copy(lang, { ar: "حالة المطابقة", en: "Match status", ur: "میچ کی حالت" })}
                    </p>
                    <p className="mt-1 font-semibold">{t(matchStatusLabelKey(match.status))}</p>
                  </div>
                  <span className={`grid size-9 place-items-center rounded-full ${match.status === MatchStatus.ACCEPTED ? "bg-success-tint text-success" : match.status === MatchStatus.REJECTED ? "bg-error-tint text-error" : "bg-warn-tint text-warn"}`}>
                    {match.status === MatchStatus.ACCEPTED ? <CheckCircle2 className="size-4" /> : match.status === MatchStatus.REJECTED ? <XCircle className="size-4" /> : <Sparkles className="size-4" />}
                  </span>
                </div>
              </section>
            )}
          </aside>
        </div>
      </div>
    </section>
  );
}

function MetaItem({ Icon, label, value }) {
  return (
    <div className="flex min-h-16 items-center gap-3 rounded-2xl bg-stone-50 px-4 py-3">
      <Icon className="size-4 shrink-0 text-primary" />
      <div className="min-w-0">
        <p className="text-[11px] text-muted-foreground">{label}</p>
        <p className="mt-0.5 truncate text-sm font-semibold">{value}</p>
      </div>
    </div>
  );
}

// Phase 4 Part 5 (Task B.2): the claim entry point, relocated here from
// SmartSearch.jsx's result cards (Phase 4 Part 3) - presented as the
// natural next step after reviewing this report's own details, rather
// than a repeat of the old inline search-result buttons.
function ClaimAction({ t, tr, claim, onStart, onSelectReport, onConfirm, onCancel }) {
  const busy = claim && !["error", "confirming", "picking"].includes(claim.status);

  return (
    <div>
      <p className="mb-3 text-xs leading-relaxed text-muted-foreground">
        {tr({
          ar: "اتخذ الإجراء المناسب بعد مراجعة تفاصيل هذا البلاغ.",
          en: "Take the appropriate action after reviewing this report's details.",
          ur: "اس رپورٹ کی تفصیلات دیکھنے کے بعد مناسب کارروائی اختیار کریں۔",
        })}
      </p>
      <div className="grid grid-cols-2 gap-2">
        <button
          type="button"
          onClick={() => onStart("mine")}
          disabled={busy}
          className="flex min-h-12 w-full cursor-pointer items-center justify-center gap-2 rounded-2xl bg-success-tint px-3 text-sm font-bold text-success transition-all duration-200 hover:-translate-y-0.5 hover:opacity-90 hover:shadow-sm disabled:pointer-events-none disabled:opacity-50"
        >
          <Check className="size-4" />
          {t("claimMineCta")}
        </button>
        <button
          type="button"
          onClick={() => onStart("not-mine")}
          disabled={busy}
          className="flex min-h-12 w-full cursor-pointer items-center justify-center gap-2 rounded-2xl border border-border px-3 text-sm font-bold text-muted-foreground transition-all duration-200 hover:-translate-y-0.5 hover:bg-stone-100 hover:text-foreground disabled:pointer-events-none disabled:opacity-50"
        >
          <UserX className="size-4" />
          {t("claimNotMineCta")}
        </button>
      </div>

      {claim && (
        <ClaimPanel claim={claim} tr={tr} onSelectReport={onSelectReport} onConfirm={onConfirm} onCancel={onCancel} />
      )}
    </div>
  );
}

// Phase 4 Part 5: relocated, not duplicated, from SmartSearch.jsx's own
// ClaimPanel (Phase 4 Part 3) - covers every state startClaim/confirmClaim
// can produce, adapted to this page's sidebar layout instead of an inline
// card footer.
function ClaimPanel({ claim, tr, onSelectReport, onConfirm, onCancel }) {
  const isMine = claim.action === "mine";

  if (claim.status === "loading") {
    return (
      <div className="mt-3 flex items-center gap-2 text-xs text-muted-foreground">
        <Loader2 className="size-3.5 animate-spin" />
        {tr({ ar: "جارٍ التحقق من بلاغاتك…", en: "Checking your reports…", ur: "آپ کی رپورٹس چیک ہو رہی ہیں…" })}
      </div>
    );
  }

  // Phase 4 Part 8 (Task B): "not my item" is a simple confirmation only
  // - no picker, no report-eligibility lookup of any kind. Reached
  // directly from startClaim, for every user regardless of what reports
  // (if any) they own.
  if (claim.status === "confirming") {
    return (
      <div className="mt-3 border-t border-border pt-4">
        <p className="mb-3 text-xs leading-relaxed text-muted-foreground">
          {tr({
            ar: "هل أنت متأكد أن هذا ليس غرضك؟",
            en: "Are you sure this isn't your item?",
            ur: "کیا آپ کو یقین ہے کہ یہ آپ کی چیز نہیں ہے؟",
          })}
        </p>
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

  if (claim.status === "picking") {
    return (
      <div className="mt-3 border-t border-border pt-4">
        <p className="mb-2 text-xs font-semibold">
          {tr({
            ar: "أي من بلاغاتك يتعلق بهذا؟",
            en: "Which of your reports is this?",
            ur: "یہ آپ کی کون سی رپورٹ سے متعلق ہے؟",
          })}
        </p>
        <div className="mb-3 space-y-1.5">
          {claim.eligible.map((eligibleReport) => (
            <label key={eligibleReport.id} className="flex items-center gap-2 text-xs cursor-pointer">
              <input
                type="radio"
                name="claim-report"
                checked={claim.selectedReportId === eligibleReport.id}
                onChange={() => onSelectReport(eligibleReport.id)}
              />
              <span className="truncate">
                {reportHeadingTitle(eligibleReport, null) || eligibleReport.id}
              </span>
            </label>
          ))}
          {/* Phase 4 Part 7 (Task A): always present, distinct from every
              real report - lets the user say none of their existing
              reports actually relate to this item. Reuses the exact
              zero-eligible-reports contact path (Phase 4 Part 6) via
              ownReportId: null - see confirmClaim. */}
          <label className="flex items-center gap-2 text-xs cursor-pointer border-t border-border pt-1.5 mt-1.5">
            <input
              type="radio"
              name="claim-report"
              checked={claim.selectedReportId == null}
              onChange={() => onSelectReport(null)}
            />
            <span className="truncate text-muted-foreground">
              {tr({
                ar: "لا شيء من هذه — لم أرفع بلاغًا لهذا الغرض بعد",
                en: "None of these — I haven't reported this item yet",
                ur: "ان میں سے کوئی نہیں — میں نے ابھی تک اس چیز کی رپورٹ نہیں کی",
              })}
            </span>
          </label>
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
      <div className="mt-3 flex items-center gap-2 text-xs text-muted-foreground">
        <Loader2 className="size-3.5 animate-spin" />
        {tr({ ar: "جارٍ التنفيذ…", en: "Submitting…", ur: "جمع ہو رہا ہے…" })}
      </div>
    );
  }

  if (claim.status === "success") {
    return (
      <div className="mt-3 space-y-1.5">
        <div className="flex items-center gap-2 text-xs font-semibold text-success">
          <Check className="size-3.5" />
          {isMine
            ? tr({
                ar: "تم! جارٍ نقلك إلى صفحة التواصل…",
                en: "Confirmed! Taking you to the contact page…",
                ur: "تصدیق ہو گئی! رابطہ صفحہ کی طرف لے جایا جا رہا ہے…",
              })
            : tr({
                ar: "تم الحفظ. جارٍ إعادتك إلى نتائج البحث…",
                en: "Got it. Taking you back to search…",
                ur: "محفوظ ہو گیا۔ آپ کو تلاش کی طرف واپس لے جایا جا رہا ہے…",
              })}
        </div>
        {isMine && claim.noOwnReport && (
          <p className="text-[11px] leading-relaxed text-muted-foreground">
            {tr({
              ar: "لأنه لا يوجد لديك بلاغ مطابق، لن تظهر هذه المطابقة في لوحتي الطرفين - لكن يمكنك التواصل الآن، وتم إخطار صاحب البلاغ.",
              en: "Since you don't have a matching report of your own, this won't appear as a linked match in either Dashboard — but you can contact them now, and the report owner has been notified.",
              ur: "چونکہ آپ کے پاس مماثل رپورٹ نہیں ہے، یہ دونوں ڈیش بورڈز میں منسلک میچ کے طور پر ظاہر نہیں ہوگا - لیکن آپ ابھی رابطہ کر سکتے ہیں، اور رپورٹ کے مالک کو مطلع کر دیا گیا ہے۔",
            })}{" "}
            {/* Phase 4 Part 7 (Task A, design point 4, optional): reuses
                the exact "Create one" wording/link ClaimPanel's own
                no-eligible state already uses - never a blocking step,
                just an offer for the full both-parties experience later. */}
            <Link to="/report" className="font-semibold text-primary hover:underline">
              {tr({ ar: "أنشئ بلاغًا لهذا الغرض", en: "Create a report for this item", ur: "اس چیز کی رپورٹ بنائیں" })}
            </Link>
          </p>
        )}
      </div>
    );
  }

  if (claim.status === "error") {
    return (
      <div className="mt-3">
        <p className="mb-1.5 text-xs text-error">{claim.error}</p>
        <button type="button" onClick={onCancel} className="text-xs font-semibold text-muted-foreground hover:text-foreground">
          {tr({ ar: "إغلاق", en: "Dismiss", ur: "برخاست کریں" })}
        </button>
      </div>
    );
  }

  return null;
}
