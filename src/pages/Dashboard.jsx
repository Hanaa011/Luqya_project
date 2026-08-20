import { useEffect, useMemo, useState } from "react";
import { Link } from "react-router-dom";
import {
  AlertCircle,
  Loader2,
} from "lucide-react";

import { useI18n } from "../lib/useI18n";
import { useAuth } from "../lib/useAuth";
import { listMatches } from "../api/matches";
import { fetchMyReports } from "../lib/myReports";
import { MatchStatus, ReportStatus, ReportType, matchStatusLabelKey, reportStatusLabelKey } from "../api/enums";

function copy(lang, values) {
  return values[lang] ?? values.en;
}

function reportSummary(report, fallback) {
  return report?.description || report?.aiObjectType || fallback;
}

function formatDate(value, lang) {
  if (!value) return "—";
  return new Date(value).toLocaleDateString(lang, { day: "numeric", month: "short", year: "numeric" });
}

export default function Dashboard() {
  const { t, lang } = useI18n();
  const { userId } = useAuth();

  const [loadingReports, setLoadingReports] = useState(true);
  const [reportsError, setReportsError] = useState(false);
  const [reports, setReports] = useState([]);
  const [myReportIds, setMyReportIds] = useState(new Set());

  const [loadingMatches, setLoadingMatches] = useState(true);
  const [matchesError, setMatchesError] = useState(false);
  const [matches, setMatches] = useState([]);
  const [matchWindowPartial, setMatchWindowPartial] = useState(false);
  const [showAllPending, setShowAllPending] = useState(false);

  useEffect(() => {
    document.title = "Dashboard — Luqya";
  }, []);

  useEffect(() => {
    let cancelled = false;

    if (!userId) {
      Promise.resolve().then(() => {
        if (!cancelled) setLoadingReports(false);
      });
      return () => {
        cancelled = true;
      };
    }

    Promise.resolve().then(() => {
      if (cancelled) return;
      setLoadingReports(true);
      setReportsError(false);
    });

    fetchMyReports({ userId, maxResultCount: 500 })
      .then((result) => {
        if (cancelled) return;
        if (!result.reliable) {
          setReportsError(true);
          setReports([]);
          setMyReportIds(new Set());
          return;
        }

        const mine = result.reports ?? [];
        setReports(mine);
        setMyReportIds(new Set(mine.map((report) => report.id)));
      })
      .catch(() => {
        if (cancelled) return;
        setReportsError(true);
        setReports([]);
        setMyReportIds(new Set());
      })
      .finally(() => !cancelled && setLoadingReports(false));

    return () => {
      cancelled = true;
    };
  }, [userId]);

  useEffect(() => {
    let cancelled = false;

    if (!userId || myReportIds.size === 0) {
      Promise.resolve().then(() => {
        if (!cancelled) {
          setLoadingMatches(false);
          setMatches([]);
        }
      });
      return () => {
        cancelled = true;
      };
    }

    Promise.resolve().then(() => {
      if (cancelled) return;
      setLoadingMatches(true);
      setMatchesError(false);
    });

    // MatchAppService currently has no report/owner filter. We therefore
    // cross-reference the newest window against the user's verified report
    // ids. Importantly, we keep ALL statuses here; only the attention section
    // filters to Pending. This fixes the old mismatch where accepted/rejected
    // matches vanished from Dashboard while still appearing on report details.
    listMatches({ maxResultCount: 200, sorting: "creationTime desc" })
      .then((result) => {
        if (cancelled) return;
        const items = result?.items ?? [];
        setMatchWindowPartial((result?.totalCount ?? items.length) > items.length);
        setMatches(
          items.filter((match) => myReportIds.has(match.lostReportId) || myReportIds.has(match.foundReportId))
        );
      })
      .catch(() => {
        if (cancelled) return;
        setMatchesError(true);
        setMatches([]);
      })
      .finally(() => !cancelled && setLoadingMatches(false));

    return () => {
      cancelled = true;
    };
  }, [myReportIds, userId]);

  const activeReports = useMemo(
    () => reports.filter((report) => report.status !== ReportStatus.CLOSED).slice(0, 6),
    [reports]
  );
  const resolvedReports = useMemo(
    () => reports.filter((report) => report.status === ReportStatus.CLOSED).slice(0, 4),
    [reports]
  );
  const pendingMatches = useMemo(
    () => matches.filter((match) => match.status === MatchStatus.PENDING).slice(0, 6),
    [matches]
  );
  const decidedMatches = useMemo(
    () => matches.filter((match) => match.status !== MatchStatus.PENDING).slice(0, 6),
    [matches]
  );
  const visiblePendingMatches = showAllPending ? pendingMatches : pendingMatches.slice(0, 2);
  const hiddenPendingCount = Math.max(0, pendingMatches.length - 2);

  const counts = useMemo(
    () => ({
      total: reports.length,
      active: reports.filter((report) => report.status !== ReportStatus.CLOSED).length,
      attention: matches.filter((match) => match.status === MatchStatus.PENDING).length,
      resolved: reports.filter((report) => report.status === ReportStatus.CLOSED).length,
    }),
    [matches, reports]
  );



  function otherReportId(match) {
    return myReportIds.has(match.lostReportId) ? match.foundReportId : match.lostReportId;
  }

  return (
    <section className="py-10 sm:py-14 lg:py-20">
      <div className="mx-auto max-w-7xl px-4 sm:px-6">
        <header className="mb-8 max-w-2xl sm:mb-10">
          <p className="text-[11px] font-mono tracking-[0.18em] text-primary">
            {copy(lang, {
              ar: "لُقيا · لوحة التحكم",
              en: "Luqya · Dashboard",
              ur: "لُقیا · ڈیش بورڈ",
            })}
          </p>
          <h1 className="mt-2 font-display text-4xl font-extrabold tracking-tight sm:text-5xl">
            {t("navDashboard")}
          </h1>
          <p className="mt-3 text-base leading-relaxed text-muted-foreground sm:text-lg">
            {copy(lang, {
              ar: "تابع بلاغاتك والمطابقات المهمة من مكان واحد.",
              en: "Keep track of your reports and important matches in one place.",
              ur: "اپنی رپورٹس اور اہم میچز ایک جگہ دیکھیں۔",
            })}
          </p>
        </header>

        {reportsError ? (
          <ErrorState
            text={copy(lang, {
              ar: "تعذّر التحقق من بلاغاتك الآن. حاول تحديث الصفحة أو تسجيل الدخول مرة أخرى.",
              en: "Couldn't verify your reports right now. Refresh the page or sign in again.",
              ur: "آپ کی رپورٹس کی تصدیق نہیں ہو سکی۔ صفحہ ریفریش کریں یا دوبارہ لاگ ان کریں۔",
            })}
          />
        ) : loadingReports ? (
          <LoadingState text={copy(lang, { ar: "جارٍ تجهيز لوحة التحكم...", en: "Preparing your dashboard...", ur: "ڈیش بورڈ تیار ہو رہا ہے..." })} />
        ) : (
          <>
            <div className="mb-10 grid grid-cols-2 gap-px overflow-hidden rounded-[1.75rem] border border-border bg-border lg:grid-cols-4">
              <SummaryItem label={copy(lang, { ar: "كل البلاغات", en: "All reports", ur: "تمام رپورٹس" })} value={counts.total} />
              <SummaryItem label={copy(lang, { ar: "نشطة", en: "Active", ur: "فعال" })} value={counts.active} />
              <SummaryItem label={copy(lang, { ar: "تحتاج انتباهك", en: "Needs attention", ur: "توجہ درکار" })} value={counts.attention} highlight />
              <SummaryItem label={copy(lang, { ar: "مغلقة", en: "Resolved", ur: "حل شدہ" })} value={counts.resolved} />
            </div>

            <WorkflowSection
              kicker={copy(lang, { ar: "يحتاج إجراء", en: "Action required", ur: "کارروائی درکار" })}
              tone="attention"
              title={copy(lang, { ar: "تحتاج انتباهك", en: "Needs your attention", ur: "آپ کی توجہ درکار" })}
              description={copy(lang, {
                ar: "مطابقات بانتظار قرارك.",
                en: "Matches waiting for your decision.",
                ur: "وہ میچز جو آپ کے فیصلے کے منتظر ہیں۔",
              })}
            >
              {loadingMatches ? (
                <LoadingState compact text={copy(lang, { ar: "جارٍ تحميل المطابقات...", en: "Loading matches...", ur: "میچز لوڈ ہو رہے ہیں..." })} />
              ) : matchesError ? (
                <ErrorState compact text={copy(lang, { ar: "تعذّر تحميل المطابقات.", en: "Couldn't load matches.", ur: "میچز لوڈ نہیں ہو سکے۔" })} />
              ) : pendingMatches.length > 0 ? (
                <div>
                  <div className="grid gap-3 md:grid-cols-2">
                    {visiblePendingMatches.map((match) => (
                      <PendingMatchCard
                        key={match.id}
                        match={match}
                        href={`/match/${otherReportId(match)}`}
                        lang={lang}
                        t={t}
                      />
                    ))}
                  </div>

                  {hiddenPendingCount > 0 && (
                    <div className="mt-4 flex justify-center">
                      <button
                        type="button"
                        onClick={() => setShowAllPending((current) => !current)}
                        className="min-h-10 rounded-full px-4 text-sm font-semibold text-primary transition-colors hover:bg-primary/[0.05] focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
                        aria-expanded={showAllPending}
                      >
                        {showAllPending
                          ? copy(lang, { ar: "عرض أقل", en: "Show less", ur: "کم دکھائیں" })
                          : copy(lang, {
                              ar: `عرض ${hiddenPendingCount} مطابقة أخرى`,
                              en: `Show ${hiddenPendingCount} more ${hiddenPendingCount === 1 ? "match" : "matches"}`,
                              ur: `${hiddenPendingCount} مزید میچ دکھائیں`,
                            })}
                      </button>
                    </div>
                  )}
                </div>
              ) : (
                <EmptyState
                  title={copy(lang, { ar: "لا يوجد شيء يحتاج قرارك الآن", en: "You're all caught up", ur: "فی الحال کوئی فیصلہ باقی نہیں" })}
                  text={copy(lang, {
                    ar: "أي مطابقة سابقة ستظل ظاهرة في قسم المطابقات الأخيرة أدناه.",
                    en: "Any previous match still remains visible in Recent Matches below.",
                    ur: "پچھلے میچز نیچے Recent Matches میں نظر آتے رہیں گے۔",
                  })}
                />
              )}
            </WorkflowSection>

            <WorkflowSection
              kicker={copy(lang, { ar: "قيد المتابعة", en: "In progress", ur: "جاری" })}
              tone="active"
              title={copy(lang, { ar: "البلاغات النشطة", en: "Active reports", ur: "فعال رپورٹس" })}
              description={copy(lang, {
                ar: "البلاغات المفتوحة أو التي لديها تطابق ولم تُغلق بعد.",
                en: "Open or matched reports that haven't been closed yet.",
                ur: "کھلی یا میچ شدہ رپورٹس جو ابھی بند نہیں ہوئیں۔",
              })}
            >
              {activeReports.length > 0 ? (
                <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-3">
                  {activeReports.map((report) => (
                    <ActiveReportCard key={report.id} report={report} lang={lang} t={t} />
                  ))}
                </div>
              ) : (
                <EmptyState
                  title={copy(lang, { ar: "لا توجد بلاغات نشطة", en: "No active reports", ur: "کوئی فعال رپورٹ نہیں" })}
                  text={copy(lang, { ar: "أنشئ بلاغًا جديدًا عندما تحتاج للمساعدة.", en: "Create a new report whenever you need help.", ur: "ضرورت کے وقت نئی رپورٹ بنائیں۔" })}
                />
              )}
            </WorkflowSection>

            <WorkflowSection
              kicker={copy(lang, { ar: "تم اتخاذ قرار", en: "Decision history", ur: "فیصلہ ہو چکا" })}
              tone="decided"
              title={copy(lang, { ar: "المطابقات الأخيرة", en: "Recent matches", ur: "حالیہ میچز" })}
              description={copy(lang, {
                ar: "المطابقات التي سبق واتخذت قرارًا بشأنها.",
                en: "Matches you have already accepted or rejected.",
                ur: "وہ میچز جن کے بارے میں آپ پہلے فیصلہ کر چکے ہیں۔",
              })}
            >
              {decidedMatches.length > 0 ? (
                <div className="border-y border-border">
                  {decidedMatches.map((match, index) => (
                    <DecisionMatchRow
                      key={match.id}
                      match={match}
                      href={`/match/${otherReportId(match)}`}
                      lang={lang}
                      t={t}
                      last={index === decidedMatches.length - 1}
                    />
                  ))}
                </div>
              ) : (
                <EmptyState
                  title={copy(lang, { ar: "لا توجد مطابقات محسومة بعد", en: "No decided matches yet", ur: "ابھی کوئی فیصلہ شدہ میچ نہیں" })}
                  text={copy(lang, {
                    ar: "بعد قبول أو رفض أي مطابقة، ستظهر هنا بدلًا من قسم تحتاج انتباهك.",
                    en: "After you accept or reject a match, it will appear here instead of Needs your attention.",
                    ur: "کسی میچ کو قبول یا مسترد کرنے کے بعد وہ توجہ والے حصے کے بجائے یہاں نظر آئے گا۔",
                  })}
                />
              )}

              {matchWindowPartial && (
                <p className="mt-3 text-xs leading-relaxed text-muted-foreground">
                  {copy(lang, {
                    ar: "ملاحظة: واجهة المطابقات لا تدعم التصفية حسب المستخدم أو البلاغ، لذلك هذه الواجهة تربط أحدث النتائج ببلاغاتك في المتصفح. المطابقات الأقدم من نافذة النتائج الحالية قد لا تظهر.",
                    en: "Note: the matches endpoint has no report/owner filter, so this view cross-references the newest returned window in the browser. Older matches outside that window may not appear.",
                    ur: "نوٹ: matches endpoint میں رپورٹ/مالک فلٹر نہیں ہے، اس لیے یہ ویو تازہ ترین نتائج کو براؤزر میں آپ کی رپورٹس سے ملاتا ہے۔ پرانے میچز موجودہ ونڈو سے باہر ہو سکتے ہیں۔",
                  })}
                </p>
              )}
            </WorkflowSection>

            {resolvedReports.length > 0 && (
              <WorkflowSection
                kicker={copy(lang, { ar: "مكتمل", en: "Completed", ur: "مکمل" })}
                tone="resolved"
                title={copy(lang, { ar: "البلاغات المغلقة", en: "Resolved reports", ur: "حل شدہ رپورٹس" })}
                description={copy(lang, {
                  ar: "بلاغات انتهت رحلتها ولم تعد تحتاج إجراءً.",
                  en: "Reports whose workflow is complete and no longer needs action.",
                  ur: "وہ رپورٹس جن کا عمل مکمل ہو چکا ہے اور مزید کارروائی درکار نہیں۔",
                })}
              >
                <div className="grid gap-3 sm:grid-cols-2">
                  {resolvedReports.map((report) => (
                    <ArchivedReportCard key={report.id} report={report} lang={lang} t={t} />
                  ))}
                </div>
              </WorkflowSection>
            )}


          </>
        )}
      </div>
    </section>
  );
}

function SummaryItem({ label, value, highlight = false }) {
  return (
    <div className="bg-card p-5 sm:p-6">
      <p className={`text-[11px] font-mono uppercase tracking-widest ${highlight ? "text-warn" : "text-muted-foreground"}`}>{label}</p>
      <p className="mt-2 font-display text-3xl font-extrabold tracking-tight sm:text-4xl">{value}</p>
    </div>
  );
}

function WorkflowSection({ kicker, tone = "active", title, description, children }) {
  const toneStyles = {
    attention: {
      rail: "bg-warn",
      kicker: "text-warn",
    },
    active: {
      rail: "bg-primary",
      kicker: "text-primary",
    },
    decided: {
      rail: "bg-success",
      kicker: "text-success",
    },
    resolved: {
      rail: "bg-muted-foreground/45",
      kicker: "text-muted-foreground",
    },
  };

  const styles = toneStyles[tone] ?? toneStyles.active;

  return (
    <section className="border-t border-border py-8 first:border-t-0 sm:py-10">
      <div className="mb-5 flex items-start gap-3 sm:gap-4">
        <span className={`mt-1 h-11 w-1 shrink-0 rounded-full ${styles.rail}`} />
        <div>
          <p className={`text-[10px] font-bold tracking-[0.14em] ${styles.kicker}`}>
            {kicker}
          </p>
          <h2 className="mt-1 font-display text-xl font-bold tracking-tight sm:text-2xl">
            {title}
          </h2>
          <p className="mt-1 max-w-3xl text-sm leading-relaxed text-muted-foreground">
            {description}
          </p>
        </div>
      </div>

      {children}
    </section>
  );
}

function ActiveReportCard({ report, lang, t }) {
  const isLost = report.type === ReportType.LOST;

  return (
    <Link
      to={`/match/${report.id}`}
      className="group relative overflow-hidden rounded-2xl border border-border bg-card p-4 transition-all hover:-translate-y-0.5 hover:border-primary/25 hover:shadow-soft sm:p-5"
    >
      <span
        className={`absolute inset-y-0 start-0 w-1 ${
          isLost ? "bg-warn/70" : "bg-success/70"
        }`}
      />

      <div className="flex items-center justify-between gap-3">
        <span
          className={`inline-flex rounded-full px-2.5 py-1 text-[11px] font-bold ${
            isLost
              ? "bg-warn-tint text-warn"
              : "bg-success-tint text-success"
          }`}
        >
          {isLost ? t("lost") : t("found")}
        </span>

        <span className="text-[11px] font-medium text-muted-foreground">
          {t(reportStatusLabelKey(report.status))}
        </span>
      </div>

      <p className="mt-4 line-clamp-2 min-h-10 text-sm font-semibold leading-relaxed sm:text-[15px]">
        {reportSummary(report, t("browseTitle"))}
      </p>

      <p className="mt-3 text-xs text-muted-foreground">
        {formatDate(report.creationTime, lang)}
      </p>
    </Link>
  );
}

function ArchivedReportCard({ report, lang, t }) {
  const isLost = report.type === ReportType.LOST;

  return (
    <Link
      to={`/match/${report.id}`}
      className="group rounded-2xl border border-dashed border-border bg-muted/20 px-4 py-4 transition-colors hover:bg-muted/35 sm:px-5"
    >
      <div className="flex items-center justify-between gap-3">
        <span className="text-[11px] font-bold text-muted-foreground">
          {isLost ? t("lost") : t("found")}
        </span>
        <span className="rounded-full bg-muted px-2.5 py-1 text-[10px] font-semibold text-muted-foreground">
          {t(reportStatusLabelKey(report.status))}
        </span>
      </div>

      <p className="mt-3 truncate text-sm font-semibold text-foreground/75">
        {reportSummary(report, t("browseTitle"))}
      </p>

      <p className="mt-2 text-xs text-muted-foreground">
        {formatDate(report.creationTime, lang)}
      </p>
    </Link>
  );
}

function PendingMatchCard({ match, href, lang, t }) {
  const score =
    match.similarityScore != null
      ? Math.round(Number(match.similarityScore))
      : null;

  return (
    <Link
      to={href}
      className="group relative overflow-hidden rounded-2xl border border-warn/20 bg-warn-tint/[0.12] p-4 transition-all hover:-translate-y-0.5 hover:border-warn/35 hover:shadow-soft sm:p-5"
    >
      <span className="absolute inset-y-0 start-0 w-1 bg-warn/75" />

      <div className="flex items-start justify-between gap-4">
        <div className="min-w-0">
          <span className="inline-flex rounded-full bg-warn-tint px-2.5 py-1 text-[10px] font-bold text-warn">
            {t(matchStatusLabelKey(match.status))}
          </span>

          <p className="mt-3 text-sm font-semibold sm:text-[15px]">
            {copy(lang, {
              ar: "مطابقة محتملة تحتاج قرارك",
              en: "Potential match needs your decision",
              ur: "ممکنہ میچ آپ کے فیصلے کا منتظر ہے",
            })}
          </p>

          <p className="mt-1.5 text-xs text-muted-foreground">
            {formatDate(match.creationTime, lang)}
          </p>
        </div>

        {score != null && (
          <div className="shrink-0 text-end">
            <p className="font-display text-2xl font-extrabold tracking-tight text-primary sm:text-3xl">
              {score}%
            </p>
            <p className="mt-0.5 text-[10px] text-muted-foreground">
              {copy(lang, { ar: "ثقة", en: "confidence", ur: "اعتماد" })}
            </p>
          </div>
        )}
      </div>
    </Link>
  );
}

function DecisionMatchRow({ match, href, lang, t, last = false }) {
  const score =
    match.similarityScore != null
      ? Math.round(Number(match.similarityScore))
      : null;

  const accepted = match.status === MatchStatus.ACCEPTED;
  const statusTone = accepted
    ? "bg-success-tint text-success"
    : "bg-error-tint text-error";

  return (
    <Link
      to={href}
      className={`group flex min-h-[70px] items-center gap-4 px-1 py-3.5 transition-colors hover:bg-primary/[0.02] sm:px-3 ${
        last ? "" : "border-b border-border"
      }`}
    >
      <span
        className={`size-2.5 shrink-0 rounded-full ${
          accepted ? "bg-success" : "bg-error"
        }`}
      />

      <div className="min-w-0 flex-1">
        <div className="flex flex-wrap items-center gap-2">
          <p className="text-sm font-semibold">
            {copy(lang, {
              ar: accepted ? "مطابقة تم قبولها" : "مطابقة تم رفضها",
              en: accepted ? "Accepted match" : "Rejected match",
              ur: accepted ? "منظور شدہ میچ" : "مسترد شدہ میچ",
            })}
          </p>

          <span className={`rounded-full px-2 py-0.5 text-[10px] font-bold ${statusTone}`}>
            {t(matchStatusLabelKey(match.status))}
          </span>
        </div>

        <p className="mt-1 text-xs text-muted-foreground">
          {formatDate(match.creationTime, lang)}
        </p>
      </div>

      {score != null && (
        <p className="shrink-0 font-display text-lg font-bold text-primary sm:text-xl">
          {score}%
        </p>
      )}
    </Link>
  );
}

function LoadingState({ text, compact = false }) {
  return (
    <div className={`flex items-center gap-2 text-sm text-muted-foreground ${compact ? "py-5" : "justify-center py-20"}`}>
      <Loader2 className="size-4 animate-spin" />
      {text}
    </div>
  );
}

function ErrorState({ text, compact = false }) {
  return (
    <div className={`flex items-center gap-2.5 rounded-2xl bg-error-tint px-4 py-3 text-sm text-error ${compact ? "" : "mb-10"}`}>
      <AlertCircle className="size-4 shrink-0" />
      {text}
    </div>
  );
}

function EmptyState({ title, text }) {
  return (
    <div className="rounded-2xl border border-dashed border-border bg-card px-5 py-5">
      <p className="text-sm font-semibold">{title}</p>
      <p className="mt-1 text-xs leading-relaxed text-muted-foreground">{text}</p>
    </div>
  );
}
