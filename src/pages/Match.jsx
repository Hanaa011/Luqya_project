import { useEffect, useMemo, useRef, useState } from "react";
import { Link, useLocation, useNavigate, useParams, useSearchParams } from "react-router-dom";
import {
  AlertCircle,
  ArrowUpRight,
  CalendarDays,
  Check,
  CheckCircle2,
  Clock3,
  ExternalLink,
  Loader2,
  MailCheck,
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
import { openConversation } from "../api/conversations";
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
  const fromSmartSearch = searchParams.get("source") === "smart-search";
  const alternativesRequested = searchParams.get("alternatives") === "1";
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
  const smartSearchState = location.state?.smartSearchState ?? null;
  const claimableScore = typeof navScore === "number" && Number.isFinite(navScore) ? navScore : null;
  const [claim, setClaim] = useState(null);
  const [openingConversation, setOpeningConversation] = useState(false);
  const [conversationError, setConversationError] = useState(null);

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
    if (
      !alternativesRequested ||
      !report ||
      !myReportIds.has(report.id) ||
      matchCandidates.length === 0
    ) {
      return;
    }

    setShowMatchCandidates(true);
  }, [
    alternativesRequested,
    matchCandidates.length,
    myReportIds,
    report,
  ]);

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
        const ownsLoadedReport = ids.has(reportData.id);
        const primaryMatch =
          requestedMatch ??
          (ownsLoadedReport
            ? relatedMatches.find((item) => item.status === MatchStatus.ACCEPTED) ??
              relatedMatches.find((item) => item.status === MatchStatus.PENDING)
            : relatedMatches.find((item) => item.status === MatchStatus.PENDING) ??
              relatedMatches.find((item) => item.status === MatchStatus.ACCEPTED)) ??
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

  const currentAcceptedCandidate = useMemo(
    () =>
      matchCandidates.find(
        (candidate) => candidate.match.status === MatchStatus.ACCEPTED
      ) ?? null,
    [matchCandidates]
  );

  const alternativeCandidates = useMemo(
    () =>
      matchCandidates.filter(
        (candidate) => candidate.match.status === MatchStatus.PENDING
      ),
    [matchCandidates]
  );

  const reviewableCandidates = currentAcceptedCandidate
    ? alternativeCandidates
    : matchCandidates.filter(
        (candidate) => candidate.match.status === MatchStatus.PENDING
      );
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

    const confirmed = window.confirm(
      copy(lang, {
        ar:
          report.status === ReportStatus.MATCHED
            ? isLost
              ? "هل تم استرداد الغرض بالفعل؟ سيتم إنهاء البلاغ وإيقاف أي مطابقات جديدة."
              : "هل تم تسليم الغرض بالفعل؟ سيتم إنهاء البلاغ وإيقاف أي مطابقات جديدة."
            : "هل تريد إنهاء هذا البلاغ؟ سيبقى محفوظًا في سجلك، لكن لن يحتاج إلى متابعة أو مطابقات جديدة.",
        en:
          report.status === ReportStatus.MATCHED
            ? isLost
              ? "Have you recovered the item? This will complete the report and stop new matches."
              : "Has the item been returned? This will complete the report and stop new matches."
            : "End this report? It will stay in your history, but it will no longer need follow-up or new matches.",
        ur:
          report.status === ReportStatus.MATCHED
            ? isLost
              ? "کیا چیز واقعی واپس مل گئی ہے؟ رپورٹ مکمل ہو جائے گی اور نئے میچ بند ہو جائیں گے۔"
              : "کیا چیز واقعی واپس کر دی گئی ہے؟ رپورٹ مکمل ہو جائے گی اور نئے میچ بند ہو جائیں گے۔"
            : "کیا آپ یہ رپورٹ ختم کرنا چاہتے ہیں؟ یہ ریکارڈ میں رہے گی، مگر مزید پیروی یا نئے میچ نہیں آئیں گے۔",
      })
    );

    if (!confirmed) return;

    setActionError(null);
    setWorkingAction("close");

    try {
      const updated = await updateReport(
        report.id,
        updatePayload(report, { status: ReportStatus.CLOSED })
      );
      setReport(updated);
    } catch {
      setActionError(
        copy(lang, {
          ar: "تعذّر إنهاء البلاغ. حاول مرة أخرى.",
          en: "Couldn't complete the report. Please try again.",
          ur: "رپورٹ مکمل نہیں ہو سکی۔ دوبارہ کوشش کریں۔",
        })
      );
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
        // Confirmation and contact are deliberately separate steps.
        // Stay on this page after confirming the match; the user chooses
        // explicitly when to open the private conversation.
        setClaim({
          action,
          status: "success",
          noOwnReport: !result?.match,
          alreadyRequested: Boolean(result?.alreadyRequested),
        });
      } else {
        // Dismissing a candidate can still return to search automatically;
        // only positive matches expose the separate Contact step.
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

  // The direct "Contact the owner/finder" action for an already-reviewed
  // match (isReviewingMatch) - same destination as confirmClaim's success
  // path, just without going through claimMatch again since access was
  // already granted on an earlier visit.
  async function openConversationAndGo() {
    setOpeningConversation(true);
    setConversationError(null);
    try {
      const conversation = await openConversation(report.id);
      navigate(`/messages/${conversation.id}`);
    } catch (err) {
      if (err instanceof ApiError && err.code === "LostFound:Reporter:0003" && claim?.action === "mine") {
        setClaim((current) => ({
          ...(current ?? {}),
          action: "mine",
          status: "pending-claim",
          alreadyRequested: Boolean(current?.alreadyRequested),
        }));
        setOpeningConversation(false);
        return;
      }

      setConversationError(
        err.message ||
          tr({
            ar: "تعذّر فتح المحادثة. حاول مرة أخرى.",
            en: "Couldn't open the conversation. Please try again.",
            ur: "بات چیت شروع نہیں ہو سکی۔ دوبارہ کوشش کریں۔",
          })
      );
      setOpeningConversation(false);
    }
  }

  function cancelClaim() {
    setClaim(null);
  }

  function backToSmartSearch() {
    navigate("/search", {
      state: smartSearchState ? { restoreSmartSearch: smartSearchState } : undefined,
    });
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
        ar: "بلاغ مفقود · راجع المطابقات لاستعادة الغرض.",
        en: "Lost report · Review matches to recover the item.",
        ur: "گمشدہ رپورٹ · چیز واپس پانے کے لیے میچز دیکھیں۔",
      })
    : copy(lang, {
        ar: "بلاغ عُثر عليه · راجع المطابقات لإعادته لصاحبه.",
        en: "Found report · Review matches to return the item.",
        ur: "ملی ہوئی چیز · مالک تک پہنچانے کے لیے میچز دیکھیں۔",
      });

  return (
    <section className="py-8 sm:py-10 lg:py-12">
      <div className="mx-auto max-w-7xl px-4 sm:px-6">
        {fromSmartSearch && (
          <button
            type="button"
            onClick={backToSmartSearch}
            className="
              mb-8 inline-flex items-center gap-1.5
              text-sm font-semibold text-muted-foreground/60
              transition-colors duration-200
              hover:text-primary
              focus-visible:outline-none
              focus-visible:text-primary
              focus-visible:underline
            "
          >
            <ArrowUpRight
              className={`size-3 ${lang === "ar" || lang === "ur" ? "" : "-scale-x-100"}`}
              strokeWidth={1.6}
              aria-hidden="true"
            />
            {copy(lang, {
              ar: "العودة إلى نتائج البحث الذكي",
              en: "Back to smart search results",
              ur: "ذہین تلاش کے نتائج پر واپس جائیں",
            })}
          </button>
        )}

        <div className={`mb-5 rounded-2xl border px-5 py-4 sm:px-6 ${typeTone}`}>
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
              <p className="text-sm font-medium text-foreground/70">{intentText}</p>
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

            {/* A confirmed match is always visible when opening your own report. */}
            {isOwnedReport && currentAcceptedCandidate && (
              <section
                className="
                  overflow-hidden rounded-[1.75rem]
                  border border-success/20 bg-card shadow-soft
                "
              >
                <div className="flex flex-wrap items-center justify-between gap-3 border-b border-border/70 px-5 py-4 sm:px-6">
                  <div className="flex items-center gap-2.5">
                    <span className="grid size-8 place-items-center rounded-xl bg-success-tint text-success">
                      <CheckCircle2 className="size-4" strokeWidth={1.8} />
                    </span>
                    <div>
                      <p className="text-[10px] font-bold tracking-[0.13em] text-success">
                        {copy(lang, {
                          ar: "المطابقة الحالية",
                          en: "Current match",
                          ur: "موجودہ میچ",
                        })}
                      </p>
                      <p className="mt-0.5 text-sm font-bold text-foreground">
                        {copy(lang, {
                          ar: "تم تأكيد هذه المطابقة",
                          en: "This match is confirmed",
                          ur: "اس میچ کی تصدیق ہو چکی ہے",
                        })}
                      </p>
                    </div>
                  </div>

                  {alternativeCandidates.length > 0 && (
                    <button
                      type="button"
                      onClick={() => setShowMatchCandidates((current) => !current)}
                      aria-expanded={showMatchCandidates}
                      aria-controls="match-candidates"
                      className="
                        inline-flex min-h-9 items-center gap-2 rounded-xl
                        px-3 text-xs font-bold text-primary
                        transition-colors hover:bg-primary/[0.05]
                        focus-visible:outline-none focus-visible:ring-2
                        focus-visible:ring-primary/15
                      "
                    >
                      <Sparkles className="size-3.5" />
                      {showMatchCandidates
                        ? copy(lang, {
                            ar: "إخفاء البدائل",
                            en: "Hide alternatives",
                            ur: "متبادل چھپائیں",
                          })
                        : copy(lang, {
                            ar: `عرض البدائل (${alternativeCandidates.length})`,
                            en: `View alternatives (${alternativeCandidates.length})`,
                            ur: `متبادل دیکھیں (${alternativeCandidates.length})`,
                          })}
                    </button>
                  )}
                </div>

                <div className="px-5 py-2 sm:px-6">
                  <CandidateMatchRow
                    candidate={currentAcceptedCandidate}
                    ownReportId={report.id}
                    lang={lang}
                    t={t}
                    current
                  />
                </div>
              </section>
            )}

            {/* Pending options only open on demand. */}
            {isOwnedReport && showMatchCandidates && (
              <section
                ref={matchCandidatesRef}
                id="match-candidates"
                className="
                  scroll-mt-24 overflow-hidden rounded-[1.75rem]
                  border border-border bg-card
                "
              >
                <div className="flex items-center justify-between gap-3 border-b border-border px-5 py-4 sm:px-6">
                  <div>
                    <p className="text-[10px] font-bold tracking-[0.13em] text-primary">
                      {copy(lang, {
                        ar: currentAcceptedCandidate ? "البدائل" : "المطابقات",
                        en: currentAcceptedCandidate ? "Alternatives" : "Matches",
                        ur: currentAcceptedCandidate ? "متبادل" : "میچز",
                      })}
                    </p>
                    <h2 className="mt-0.5 font-display text-lg font-bold sm:text-xl">
                      {copy(lang, {
                        ar: currentAcceptedCandidate
                          ? "مطابقات بديلة"
                          : "مطابقات تحتاج مراجعتك",
                        en: currentAcceptedCandidate
                          ? "Alternative matches"
                          : "Matches to review",
                        ur: currentAcceptedCandidate
                          ? "متبادل میچز"
                          : "جائزے کے لیے میچز",
                      })}
                    </h2>
                  </div>

                  <span className="rounded-full bg-primary/[0.07] px-3 py-1 text-xs font-extrabold text-primary">
                    {reviewableCandidates.length}
                  </span>
                </div>

                <div className="px-5 py-2 sm:px-6">
                  {reviewableCandidates.length > 0 ? (
                    <div className="divide-y divide-border">
                      {reviewableCandidates.map((candidate) => (
                        <CandidateMatchRow
                          key={candidate.match.id}
                          candidate={candidate}
                          ownReportId={report.id}
                          lang={lang}
                          t={t}
                          isAlternative={Boolean(currentAcceptedCandidate)}
                        />
                      ))}
                    </div>
                  ) : (
                    <div className="py-6 text-center">
                      <p className="text-sm font-semibold text-foreground">
                        {copy(lang, {
                          ar: currentAcceptedCandidate
                            ? "لا توجد بدائل أخرى"
                            : "لا توجد مطابقات تحتاج مراجعة",
                          en: currentAcceptedCandidate
                            ? "No other alternatives"
                            : "No matches need review",
                          ur: currentAcceptedCandidate
                            ? "کوئی اور متبادل نہیں"
                            : "کسی میچ کو جائزے کی ضرورت نہیں",
                        })}
                      </p>
                    </div>
                  )}
                </div>
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

                    {!currentAcceptedCandidate && reviewableCandidates.length > 0 && (
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
                          {reviewableCandidates.length}
                        </span>
                      </button>
                    )}

                    <div className="mt-1 border-t border-border/60 pt-3">
                      {report.status !== ReportStatus.CLOSED && (
                        <div className="flex items-center gap-3 py-1">
                          <span className="grid size-8 shrink-0 place-items-center rounded-lg bg-success-tint/45 text-success">
                            <CheckCircle2 className="size-4" strokeWidth={1.7} />
                          </span>

                          <div className="min-w-0 flex-1">
                            <p className="text-sm font-semibold text-foreground">
                              {copy(lang, {
                                ar: "إنهاء البلاغ",
                                en: "End report",
                                ur: "رپورٹ ختم کریں",
                              })}
                            </p>
                            <p className="mt-0.5 text-[11px] text-muted-foreground">
                              {copy(lang, {
                                ar: "إذا انتهى موضوع البلاغ، يمكنك إغلاقه.",
                                en: "If this report is finished, you can close it.",
                                ur: "اگر اس رپورٹ کا معاملہ ختم ہو گیا ہے تو آپ اسے بند کر سکتے ہیں۔",
                              })}
                            </p>
                          </div>

                          <button
                            type="button"
                            onClick={handleClose}
                            disabled={workingAction === "close"}
                            className="
                              inline-flex min-h-8 shrink-0 items-center gap-1.5 rounded-lg
                              px-2.5 text-[11px] font-bold text-success
                              transition-colors hover:bg-success-tint/45
                              focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-success/15
                              disabled:pointer-events-none disabled:opacity-50
                            "
                          >
                            {workingAction === "close" ? (
                              <Loader2 className="size-3.5 animate-spin" />
                            ) : (
                              <Check className="size-3.5" strokeWidth={1.8} />
                            )}
                            {copy(lang, {
                              ar: "إغلاق",
                              en: "Close",
                              ur: "بند کریں",
                            })}
                          </button>
                        </div>
                      )}

                      <button
                        type="button"
                        onClick={handleDelete}
                        disabled={workingAction === "delete"}
                        className="
                          mt-2 inline-flex min-h-7 items-center gap-1.5 px-0.5
                          text-[11px] font-medium text-muted-foreground/60
                          transition-colors hover:text-error
                          focus-visible:outline-none focus-visible:underline
                          disabled:pointer-events-none disabled:opacity-50
                        "
                      >
                        {workingAction === "delete" ? (
                          <Loader2 className="size-3.5 animate-spin" />
                        ) : (
                          <Trash2 className="size-3.5" strokeWidth={1.5} />
                        )}
                        {copy(lang, {
                          ar: "حذف البلاغ",
                          en: "Delete report",
                          ur: "رپورٹ حذف کریں",
                        })}
                      </button>
                    </div>
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
                      <ReviewedMatchAction
                        lang={lang}
                        status={match.status}
                        isLost={isLost}
                        workingAction={workingAction}
                        openingConversation={openingConversation}
                        conversationError={conversationError}
                        onAccept={() => handleMatchDecision("accept")}
                        onReject={() => handleMatchDecision("reject")}
                        onContact={openConversationAndGo}
                      />
                    ) : !isReviewingMatch && claimableScore != null ? (
                      <ClaimAction
                        tr={tr}
                        lang={lang}
                        score={claimableScore}
                        claim={claim}
                        onStart={startClaim}
                        onSelectReport={(reportId) => setClaim((c) => ({ ...c, selectedReportId: reportId }))}
                        onConfirm={() => confirmClaim(claim.action, claim.selectedReportId)}
                        onCancel={cancelClaim}
                        onContact={openConversationAndGo}
                        openingConversation={openingConversation}
                        conversationError={conversationError}
                      />
                    ) : !profile ? (
                      <Link to="/auth/login" className="flex min-h-12 w-full cursor-pointer items-center justify-center rounded-2xl bg-primary px-4 text-sm font-semibold text-primary-foreground transition-all duration-200 hover:-translate-y-0.5 hover:bg-primary/90 hover:shadow-md focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary/25">
                        {copy(lang, { ar: "سجّل الدخول للتواصل", en: "Log in to contact", ur: "رابطے کے لیے لاگ ان کریں" })}
                      </Link>
                    ) : null}

                  </>
                )}
              </div>
            </section>

            {match && !isOwnedReport && (
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

function CandidateMatchRow({
  candidate,
  ownReportId,
  lang,
  t,
  current = false,
  isAlternative = false,
}) {
  const { match: candidateMatch, report: candidateReport } = candidate;
  const candidateDetails = reportHeading(candidateReport, t("browseTitle"));
  const candidateScore = getMatchScore(candidateMatch);

  return (
    <Link
      to={`/match/${candidateReport.id}?from=${ownReportId}`}
      className={`
        group flex items-center gap-4 rounded-2xl px-1 py-4
        transition-all focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring
        ${current ? "hover:bg-success-tint/25" : "hover:bg-primary/[0.025]"}
      `}
    >
      <div
        className={`grid size-14 shrink-0 place-items-center rounded-2xl ${
          current
            ? "bg-success-tint text-success"
            : "bg-primary/[0.07] text-primary"
        }`}
      >
        {current ? (
          <CheckCircle2 className="size-5" strokeWidth={1.8} />
        ) : (
          <span className="font-display text-base font-extrabold">
            {candidateScore != null ? `${candidateScore}%` : "—"}
          </span>
        )}
      </div>

      <div className="min-w-0 flex-1">
        <div className="flex flex-wrap items-center gap-2">
          <h3 className="truncate font-display text-base font-bold text-foreground sm:text-lg">
            {candidateDetails.title}
          </h3>

          <span className="rounded-full bg-stone-100 px-2.5 py-1 text-[10px] font-semibold text-muted-foreground">
            {candidateReport.type === ReportType.LOST ? t("lost") : t("found")}
          </span>

          {current && (
            <span className="rounded-full bg-success-tint px-2.5 py-1 text-[10px] font-bold text-success">
              {copy(lang, {
                ar: "الحالية",
                en: "Current",
                ur: "موجودہ",
              })}
            </span>
          )}
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

          {current && candidateScore != null && (
            <span className="font-bold text-success">
              {candidateScore}%{" "}
              {copy(lang, {
                ar: "ثقة",
                en: "confidence",
                ur: "اعتماد",
              })}
            </span>
          )}
        </div>
      </div>

      <span className="hidden shrink-0 text-xs font-semibold text-primary transition-opacity group-hover:opacity-100 sm:inline">
        {copy(lang, {
          ar: current
            ? "عرض المطابقة"
            : isAlternative
              ? "مراجعة البديل"
              : "مراجعة المطابقة",
          en: current
            ? "View match"
            : isAlternative
              ? "Review alternative"
              : "Review match",
          ur: current
            ? "میچ دیکھیں"
            : isAlternative
              ? "متبادل کا جائزہ"
              : "میچ کا جائزہ",
        })}
      </span>
    </Link>
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
function FlowSteps({ step, lang }) {
  const steps = [
    copy(lang, { ar: "مراجعة", en: "Review", ur: "جائزہ" }),
    copy(lang, { ar: "تأكيد", en: "Confirm", ur: "تصدیق" }),
    copy(lang, { ar: "تواصل", en: "Contact", ur: "رابطہ" }),
  ];

  return (
    <div
      className="mb-5 grid grid-cols-3 gap-2"
      aria-label={copy(lang, {
        ar: "خطوات المطابقة",
        en: "Match steps",
        ur: "میچ کے مراحل",
      })}
    >
      {steps.map((label, index) => {
        const number = index + 1;
        const complete = number < step;
        const active = number === step;

        return (
          <div key={label} className="min-w-0">
            <div className="flex items-center gap-2">
              <span
                className={`grid size-7 shrink-0 place-items-center rounded-full text-[11px] font-extrabold transition-colors ${
                  complete
                    ? "bg-success text-white"
                    : active
                      ? "bg-primary text-primary-foreground"
                      : "bg-stone-100 text-muted-foreground"
                }`}
              >
                {complete ? (
                  <Check className="size-3.5" />
                ) : number === 3 ? (
                  <MessageCircle className="size-3.5" />
                ) : (
                  number
                )}
              </span>

              {index < steps.length - 1 && (
                <span
                  className={`h-px flex-1 ${
                    complete ? "bg-success/50" : "bg-border"
                  }`}
                />
              )}
            </div>

            <p
              className={`mt-1.5 truncate text-[10px] font-semibold ${
                active ? "text-primary" : "text-muted-foreground"
              }`}
            >
              {label}
            </p>
          </div>
        );
      })}
    </div>
  );
}

function ReviewedMatchAction({
  lang,
  status,
  isLost,
  workingAction,
  openingConversation,
  conversationError,
  onAccept,
  onReject,
  onContact,
}) {
  const isPending = status === MatchStatus.PENDING;
  const isAccepted = status === MatchStatus.ACCEPTED;
  const isRejected = status === MatchStatus.REJECTED;

  return (
    <div>
      <FlowSteps step={isAccepted ? 3 : 2} lang={lang} />

      {isPending && (
        <>
          <div className="rounded-2xl border border-primary/10 bg-primary/[0.035] p-4">
            <div className="flex items-start gap-3">
              <span className="grid size-9 shrink-0 place-items-center rounded-full bg-primary/10 text-primary">
                <Sparkles className="size-4" />
              </span>
              <div className="min-w-0">
                <p className="font-display text-base font-bold text-foreground">
                  {copy(lang, {
                    ar: "راجع المطابقة أولًا",
                    en: "Review the match first",
                    ur: "پہلے میچ کا جائزہ لیں",
                  })}
                </p>
                <p className="mt-1 text-xs leading-relaxed text-muted-foreground">
                  {copy(lang, {
                    ar: "تأكد من الوصف والموقع والتاريخ، ثم اختر ما إذا كانت المطابقة صحيحة. التواصل سيظهر بعد التأكيد فقط.",
                    en: "Check the description, location and date, then decide whether the match is correct. Contact appears only after confirmation.",
                    ur: "تفصیل، مقام اور تاریخ دیکھیں، پھر فیصلہ کریں کہ میچ درست ہے یا نہیں۔ رابطہ صرف تصدیق کے بعد ظاہر ہوگا۔",
                  })}
                </p>
              </div>
            </div>
          </div>

          <div className="mt-4 grid grid-cols-2 gap-2">
            <button
              type="button"
              onClick={onAccept}
              disabled={Boolean(workingAction)}
              className="inline-flex min-h-12 cursor-pointer items-center justify-center gap-2 rounded-2xl bg-primary px-3 text-sm font-bold text-primary-foreground transition-all duration-200 hover:-translate-y-0.5 hover:bg-primary/90 hover:shadow-sm disabled:pointer-events-none disabled:opacity-50"
            >
              {workingAction === "accept" ? (
                <Loader2 className="size-4 animate-spin" />
              ) : (
                <Check className="size-4" />
              )}
              {copy(lang, {
                ar: "تأكيد المطابقة",
                en: "Confirm match",
                ur: "میچ کی تصدیق",
              })}
            </button>

            <button
              type="button"
              onClick={onReject}
              disabled={Boolean(workingAction)}
              className="inline-flex min-h-12 cursor-pointer items-center justify-center gap-2 rounded-2xl border border-border px-3 text-sm font-bold text-muted-foreground transition-all duration-200 hover:-translate-y-0.5 hover:border-foreground/20 hover:bg-stone-100 hover:text-foreground disabled:pointer-events-none disabled:opacity-50"
            >
              <XCircle className="size-4" />
              {copy(lang, {
                ar: "ليست مطابقة",
                en: "Not a match",
                ur: "یہ میچ نہیں",
              })}
            </button>
          </div>
        </>
      )}

      {isAccepted && (
        <>
          <div className="rounded-2xl border border-success/20 bg-success-tint/55 p-4">
            <div className="flex items-start gap-3">
              <span className="grid size-9 shrink-0 place-items-center rounded-full bg-success text-white">
                <CheckCircle2 className="size-4" />
              </span>
              <div>
                <p className="font-display text-base font-bold text-foreground">
                  {copy(lang, {
                    ar: "تم تأكيد المطابقة",
                    en: "Match confirmed",
                    ur: "میچ کی تصدیق ہو گئی",
                  })}
                </p>
                <p className="mt-1 text-xs leading-relaxed text-muted-foreground">
                  {copy(lang, {
                    ar: "اكتملت المطابقة. إذا رغبت، يمكنك الآن بدء محادثة خاصة وآمنة مع الطرف الآخر.",
                    en: "The match is confirmed. You can now choose to start a private, secure conversation with the other party.",
                    ur: "میچ کی تصدیق ہو گئی۔ اب آپ چاہیں تو دوسرے فریق کے ساتھ نجی اور محفوظ گفتگو شروع کر سکتے ہیں۔",
                  })}
                </p>
              </div>
            </div>
          </div>

          <button
            type="button"
            onClick={onContact}
            disabled={openingConversation}
            className="mt-3 flex min-h-12 w-full cursor-pointer items-center justify-between gap-3 rounded-2xl bg-primary px-4 text-sm font-semibold text-primary-foreground shadow-sm transition-all duration-200 hover:-translate-y-0.5 hover:bg-primary/90 hover:shadow-md focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary/25 disabled:pointer-events-none disabled:opacity-60"
          >
            <span className="inline-flex items-center gap-3">
              {openingConversation ? (
                <Loader2 className="size-4 animate-spin" />
              ) : (
                <MessageCircle className="size-4" />
              )}
              {isLost
                ? copy(lang, {
                    ar: "بدء المحادثة مع صاحب الغرض",
                    en: "Message the owner",
                    ur: "مالک سے گفتگو شروع کریں",
                  })
                : copy(lang, {
                    ar: "بدء المحادثة مع من عثر عليه",
                    en: "Message the finder",
                    ur: "ملنے والے سے گفتگو شروع کریں",
                  })}
            </span>
            <ArrowRight className={`size-4 ${lang === "ar" || lang === "ur" ? "rotate-180" : ""}`} />
          </button>

          {conversationError && (
            <p className="mt-2 text-xs text-error">{conversationError}</p>
          )}
        </>
      )}

      {isRejected && (
        <div className="rounded-2xl border border-border bg-stone-50 p-4">
          <div className="flex items-start gap-3">
            <span className="grid size-9 shrink-0 place-items-center rounded-full bg-stone-200 text-muted-foreground">
              <XCircle className="size-4" />
            </span>
            <div>
              <p className="font-display text-base font-bold text-foreground">
                {copy(lang, {
                  ar: "تم استبعاد المطابقة",
                  en: "Match dismissed",
                  ur: "میچ مسترد کر دیا گیا",
                })}
              </p>
              <p className="mt-1 text-xs leading-relaxed text-muted-foreground">
                {copy(lang, {
                  ar: "لن يظهر خيار التواصل لهذه المطابقة.",
                  en: "Contact is unavailable for this match.",
                  ur: "اس میچ کے لیے رابطہ دستیاب نہیں ہوگا۔",
                })}
              </p>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}

// Same functionality as the first file; only the visual language below is
// taken from the second pasted design.
function ClaimAction({ tr, lang, score, claim, onStart, onSelectReport, onConfirm, onCancel, onContact, openingConversation, conversationError }) {
  const busy =
    claim &&
    !["error", "pending-claim", "confirming", "picking"].includes(
      claim.status
    );

  const step =
    (claim?.status === "success" && claim?.action === "mine") || claim?.status === "pending-claim"
      ? 3
      : 2;

  return (
    <div>
      <FlowSteps step={step} lang={lang} />

      {!claim && (
        <div className="mb-4 rounded-2xl bg-primary/[0.035] p-4">
          <div className="flex items-start justify-between gap-3">
            <p className="font-display text-base font-bold text-foreground">
              {tr({
                ar: "هل هذا البلاغ يطابق حالتك؟",
                en: "Does this report match your case?",
                ur: "کیا یہ رپورٹ آپ کی صورتحال سے ملتی ہے؟",
              })}
            </p>

            {typeof score === "number" && Number.isFinite(score) && (
              <span className="shrink-0 rounded-full bg-primary/10 px-2.5 py-1 text-xs font-extrabold text-primary">
                {Math.round(score)}%
              </span>
            )}
          </div>

          <p className="mt-1.5 text-xs leading-relaxed text-muted-foreground">
            {tr({
              ar: "راجع تفاصيل البلاغ جيدًا قبل اتخاذ القرار.",
              en: "Review the report details carefully before choosing an action.",
              ur: "فیصلہ کرنے سے پہلے رپورٹ کی تفصیلات اچھی طرح دیکھیں۔",
            })}
          </p>
        </div>
      )}

      {!claim && (
        <div className="grid grid-cols-2 gap-2">
          <button
            type="button"
            onClick={() => onStart("mine")}
            disabled={busy}
            className="flex min-h-12 w-full cursor-pointer items-center justify-center gap-2 rounded-2xl bg-primary px-3 text-sm font-bold text-primary-foreground transition-all duration-200 hover:-translate-y-0.5 hover:bg-primary/90 hover:shadow-sm disabled:pointer-events-none disabled:opacity-50"
          >
            <CheckCircle2 className="size-4" />
            {tr({
              ar: "هذا غرضي",
              en: "This is my item",
              ur: "یہ میری چیز ہے",
            })}
          </button>

          <button
            type="button"
            onClick={() => onStart("not-mine")}
            disabled={busy}
            className="flex min-h-12 w-full cursor-pointer items-center justify-center gap-2 rounded-2xl border border-border px-3 text-sm font-bold text-muted-foreground transition-all duration-200 hover:-translate-y-0.5 hover:border-foreground/20 hover:bg-stone-100 hover:text-foreground disabled:pointer-events-none disabled:opacity-50"
          >
            <UserX className="size-4" />
            {tr({
              ar: "ليس غرضي",
              en: "Not my item",
              ur: "یہ میری چیز نہیں",
            })}
          </button>
        </div>
      )}

      {claim && (
        <ClaimPanel
          claim={claim}
          tr={tr}
          onSelectReport={onSelectReport}
          onConfirm={onConfirm}
          onCancel={onCancel}
          onContact={onContact}
          openingConversation={openingConversation}
          conversationError={conversationError}
        />
      )}
    </div>
  );
}

function ClaimPanel({ claim, tr, onSelectReport, onConfirm, onCancel, onContact, openingConversation, conversationError }) {
  const isMine = claim.action === "mine";

  if (claim.status === "loading") {
    return (
      <div className="mt-3 flex items-center gap-2 rounded-xl bg-primary/[0.035] px-3 py-3 text-xs text-muted-foreground">
        <Loader2 className="size-3.5 animate-spin" />
        {tr({
          ar: "جارٍ التحقق من بلاغاتك…",
          en: "Checking your reports…",
          ur: "آپ کی رپورٹس چیک ہو رہی ہیں…",
        })}
      </div>
    );
  }

  if (claim.status === "confirming") {
    return (
      <div className="mt-3 rounded-2xl border border-border bg-stone-50 p-4">
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
            className="flex-1 rounded-xl bg-primary py-2.5 text-xs font-bold text-primary-foreground"
          >
            {tr({ ar: "تأكيد", en: "Confirm", ur: "تصدیق کریں" })}
          </button>
          <button
            type="button"
            onClick={onCancel}
            className="rounded-xl border border-border px-3 py-2.5 text-xs font-semibold"
          >
            {tr({ ar: "إلغاء", en: "Cancel", ur: "منسوخ کریں" })}
          </button>
        </div>
      </div>
    );
  }

  if (claim.status === "picking") {
    return (
      <div className="mt-3 rounded-2xl border border-border bg-card p-4">
        <p className="text-sm font-bold text-foreground">
          {tr({
            ar: "هل تريد ربط المطابقة بأحد بلاغاتك؟",
            en: "Link this match to one of your reports?",
            ur: "کیا اس میچ کو اپنی کسی رپورٹ سے جوڑنا چاہتے ہیں؟",
          })}
        </p>

        <p className="mt-1 text-xs leading-relaxed text-muted-foreground">
          {tr({
            ar: "الربط يساعدك على تتبع المطابقة في لوحتك. ويمكنك المتابعة بدون ربط إذا لم يكن لديك بلاغ مناسب.",
            en: "Linking helps you track the match in your dashboard. You can continue without linking if none applies.",
            ur: "جوڑنے سے میچ ڈیش بورڈ میں ٹریک ہوتا ہے۔ اگر کوئی مناسب رپورٹ نہ ہو تو بغیر جوڑے بھی جاری رکھ سکتے ہیں۔",
          })}
        </p>

        <div className="mt-3 space-y-2">
          {claim.eligible.map((eligibleReport) => (
            <label
              key={eligibleReport.id}
              className={`flex cursor-pointer items-center gap-3 rounded-xl border px-3 py-2.5 text-xs transition-colors ${
                claim.selectedReportId === eligibleReport.id
                  ? "border-primary/35 bg-primary/[0.04]"
                  : "border-border hover:bg-stone-50"
              }`}
            >
              <input
                type="radio"
                name="claim-report"
                checked={claim.selectedReportId === eligibleReport.id}
                onChange={() => onSelectReport(eligibleReport.id)}
                className="accent-[var(--primary)]"
              />
              <span className="min-w-0 truncate font-semibold">
                {reportHeadingTitle(eligibleReport, null) || eligibleReport.id}
              </span>
            </label>
          ))}

          <label
            className={`flex cursor-pointer items-center gap-3 rounded-xl border px-3 py-2.5 text-xs transition-colors ${
              claim.selectedReportId == null
                ? "border-primary/35 bg-primary/[0.04]"
                : "border-border hover:bg-stone-50"
            }`}
          >
            <input
              type="radio"
              name="claim-report"
              checked={claim.selectedReportId == null}
              onChange={() => onSelectReport(null)}
              className="accent-[var(--primary)]"
            />
            <span className="text-muted-foreground">
              {tr({
                ar: "لا شيء من هذه — لم أرفع بلاغًا لهذا الغرض بعد",
                en: "None of these — I haven't reported this item yet",
                ur: "ان میں سے کوئی نہیں — میں نے ابھی تک اس چیز کی رپورٹ نہیں کی",
              })}
            </span>
          </label>
        </div>

        <div className="mt-4 flex gap-2">
          <button
            type="button"
            onClick={onConfirm}
            className="flex-1 rounded-xl bg-primary py-2.5 text-xs font-bold text-primary-foreground"
          >
            {tr({
              ar: "تأكيد المطابقة",
              en: "Confirm match",
              ur: "میچ کی تصدیق",
            })}
          </button>
          <button
            type="button"
            onClick={onCancel}
            className="rounded-xl border border-border px-3 py-2.5 text-xs font-semibold"
          >
            {tr({ ar: "رجوع", en: "Back", ur: "واپس" })}
          </button>
        </div>
      </div>
    );
  }

  if (claim.status === "submitting") {
    return (
      <div className="mt-3 flex items-center gap-2 rounded-xl bg-primary/[0.035] px-3 py-3 text-xs text-muted-foreground">
        <Loader2 className="size-3.5 animate-spin" />
        {tr({
          ar: "جارٍ تأكيد المطابقة…",
          en: "Confirming match…",
          ur: "میچ کی تصدیق ہو رہی ہے…",
        })}
      </div>
    );
  }

  if (claim.status === "success") {
    if (!isMine) {
      return (
        <div className="mt-3 rounded-2xl border border-border bg-stone-50 p-4">
          <div className="flex items-start gap-3">
            <span className="grid size-9 shrink-0 place-items-center rounded-full bg-stone-200 text-muted-foreground">
              <XCircle className="size-4" />
            </span>
            <div>
              <p className="font-display text-sm font-bold text-foreground">
                {tr({ ar: "تم استبعاد البلاغ", en: "Report dismissed", ur: "رپورٹ ہٹا دی گئی" })}
              </p>
              <p className="mt-1 text-xs leading-relaxed text-muted-foreground">
                {tr({
                  ar: "تم حفظ قرارك. جارٍ إعادتك إلى نتائج البحث…",
                  en: "Your choice was saved. Returning you to search…",
                  ur: "آپ کا فیصلہ محفوظ ہو گیا۔ تلاش کی طرف واپس جا رہے ہیں…",
                })}
              </p>
            </div>
          </div>
        </div>
      );
    }

    return (
      <div className="mt-3">
        <div className="rounded-2xl border border-success/20 bg-success-tint/55 p-4">
          <div className="flex items-start gap-3">
            <span className="grid size-9 shrink-0 place-items-center rounded-full bg-success text-white">
              <CheckCircle2 className="size-4" />
            </span>
            <div>
              <p className="font-display text-sm font-bold text-foreground">
                {tr({
                  ar: "تم تأكيد المطابقة",
                  en: "Match confirmed",
                  ur: "میچ کی تصدیق ہو گئی",
                })}
              </p>
              <p className="mt-1 text-xs leading-relaxed text-muted-foreground">
                {tr({
                  ar: "المطابقة مؤكدة. يمكنك الآن اختيار بدء المحادثة مع الطرف الآخر.",
                  en: "The match is confirmed. You can now choose to start the conversation with the other party.",
                  ur: "میچ کی تصدیق ہو گئی۔ اب آپ دوسرے فریق کے ساتھ گفتگو شروع کرنے کا انتخاب کر سکتے ہیں۔",
                })}
              </p>
            </div>
          </div>
        </div>

        <button
          type="button"
          onClick={onContact}
          disabled={openingConversation}
          className="mt-3 flex min-h-12 w-full cursor-pointer items-center justify-between gap-3 rounded-2xl bg-primary px-4 text-sm font-semibold text-primary-foreground shadow-sm transition-all duration-200 hover:-translate-y-0.5 hover:bg-primary/90 hover:shadow-md disabled:pointer-events-none disabled:opacity-60"
        >
          <span className="inline-flex items-center gap-2">
            {openingConversation ? (
              <Loader2 className="size-4 animate-spin" />
            ) : (
              <MessageCircle className="size-4" />
            )}
            {tr({
              ar: "بدء المحادثة",
              en: "Start conversation",
              ur: "گفتگو شروع کریں",
            })}
          </span>
          <ArrowRight className="size-4" />
        </button>

        {conversationError && (
          <p className="mt-2 text-xs text-error">{conversationError}</p>
        )}

        {claim.noOwnReport && (
          <p className="mt-2 text-[11px] leading-relaxed text-muted-foreground">
            {tr({
              ar: "لأنه لا يوجد لديك بلاغ مطابق، لن تظهر هذه المطابقة كربط بين بلاغين في لوحة التحكم. يمكنك إنشاء بلاغ لاحقًا إذا رغبت.",
              en: "Because you don't have a matching report of your own, this won't appear as a two-report link in the dashboard. You can create a report later if you want.",
              ur: "چونکہ آپ کے پاس اپنی مماثل رپورٹ نہیں ہے، یہ ڈیش بورڈ میں دو رپورٹس کے لنک کے طور پر ظاہر نہیں ہوگا۔ آپ چاہیں تو بعد میں رپورٹ بنا سکتے ہیں۔",
            })}{" "}
            <Link to="/report" className="font-semibold text-primary hover:underline">
              {tr({ ar: "إنشاء بلاغ", en: "Create a report", ur: "رپورٹ بنائیں" })}
            </Link>
          </p>
        )}
      </div>
    );
  }

  if (claim.status === "pending-claim") {
    return (
      <div className="mt-3 rounded-2xl border border-success/20 bg-success-tint/55 p-4">
        <div className="flex items-start gap-3">
          <span className="grid size-9 shrink-0 place-items-center rounded-full bg-success/10 text-success">
            <MailCheck className="size-4" />
          </span>

          <div>
            <p className="font-display text-sm font-bold text-foreground">
              {tr({
                ar: "بانتظار تأكيد صاحب البلاغ",
                en: "Waiting for the report owner",
                ur: "رپورٹ کے مالک کی تصدیق کا انتظار ہے",
              })}
            </p>

            <p className="mt-1 text-xs leading-relaxed text-muted-foreground">
              {claim.alreadyRequested
                ? tr({
                    ar: "سبق أن أُرسل رابط تحقق إلى صاحب البلاغ عبر البريد الإلكتروني. بعد إنشاء حسابه وتأكيد البلاغ، ستتمكن من التواصل معه مباشرة عبر المحادثة.",
                    en: "A verification link was already emailed to the report owner. Once they create an account and confirm the report, you'll be able to message them directly here.",
                    ur: "رپورٹ کے مالک کو ای میل کے ذریعے تصدیقی لنک پہلے ہی بھیجا جا چکا ہے۔ اکاؤنٹ بنانے اور رپورٹ کی تصدیق کے بعد، آپ یہاں براہ راست ان سے بات کر سکیں گے۔",
                  })
                : tr({
                    ar: "تم إرسال رابط تحقق إلى صاحب البلاغ عبر البريد الإلكتروني. بعد إنشاء حسابه وتأكيد البلاغ، ستتمكن من التواصل معه مباشرة عبر المحادثة.",
                    en: "A verification link has been emailed to the report owner. Once they create an account and confirm the report, you'll be able to message them directly here.",
                    ur: "رپورٹ کے مالک کو ای میل کے ذریعے تصدیقی لنک بھیج دیا گیا ہے۔ اکاؤنٹ بنانے اور رپورٹ کی تصدیق کے بعد، آپ یہاں براہ راست ان سے بات کر سکیں گے۔",
                  })}
            </p>
          </div>
        </div>
      </div>
    );
  }

  if (claim.status === "error") {
    return (
      <div className="mt-3 rounded-xl bg-error-tint px-3 py-3">
        <p className="text-xs text-error">{claim.error}</p>
        <button
          type="button"
          onClick={onCancel}
          className="mt-2 text-xs font-semibold text-muted-foreground hover:text-foreground"
        >
          {tr({ ar: "إغلاق", en: "Dismiss", ur: "برخاست کریں" })}
        </button>
      </div>
    );
  }

  return null;
}
