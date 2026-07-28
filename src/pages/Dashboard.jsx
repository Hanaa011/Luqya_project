import { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import {
  TrendingUp,
  Package,
  Sparkles,
  Clock,
  Loader2,
  AlertCircle,
  UserCircle,
  Search,
  PlusCircle,
  Compass,
  ArrowRight,
} from "lucide-react";

import { useI18n } from "../lib/useI18n";
import { useAuth } from "../lib/useAuth";
import { listMatches } from "../api/matches";
import { fetchMyReports } from "../lib/myReports";
import { ReportStatus, MatchStatus, reportStatusLabelKey } from "../api/enums";

export default function Dashboard() {
  const { t, lang, tr } = useI18n();
  const { profile, userId } = useAuth();

  // --- The current user's own reports — there is no real server-side
  // creatorId filter (see lib/myReports.js), so this is fetched globally
  // and filtered client-side by each report's own creatorId field. ---
  const [loadingMine, setLoadingMine] = useState(true);
  const [mineError, setMineError] = useState(null);
  const [myCounts, setMyCounts] = useState({ total: 0, open: 0, matched: 0, closed: 0 });
  const [myRecent, setMyRecent] = useState([]);

  // --- Pending matches that involve one of the user's own reports ---
  // MatchAppService.GetListAsync has no owner filter (verified against
  // MatchAppService.cs — it's a plain PagedAndSortedResultRequestDto), so
  // this cross-references the same way SmartSearch already does for "my
  // own reports excluded": fetch my real report ids via the verified
  // creatorId (real per-item field, just not a query filter — see above),
  // then keep only matches whose lostReportId or
  // foundReportId is one of mine. Never another user's match data.
  const [loadingMatches, setLoadingMatches] = useState(true);
  const [myReportIds, setMyReportIds] = useState(new Set());
  const [myMatches, setMyMatches] = useState([]);

  useEffect(() => {
    document.title = "Dashboard — Luqya";
  }, []);

  useEffect(() => {
    let cancelled = false;

    if (!userId) {
      Promise.resolve().then(() => !cancelled && setLoadingMine(false));
      return () => {
        cancelled = true;
      };
    }

    Promise.resolve().then(() => !cancelled && setLoadingMine(true));

    // A single fetch, filtered client-side by the report's own `creatorId`
    // field (see lib/myReports.js for why this can't be a server-side
    // query param). Counts below are derived from this same array rather
    // than reading `totalCount` from separately "creatorId-filtered"
    // requests — those totals were previously the *global* count for
    // that status, not this user's, which was one concrete symptom of
    // the account-data-leak bug.
    fetchMyReports({ userId, maxResultCount: 500 })
      .then((recent) => {
        if (cancelled) return;
        const mine = recent.reliable ? recent.reports : [];
        setMyCounts({
          total: mine.length,
          open: mine.filter((r) => r.status === ReportStatus.OPEN).length,
          matched: mine.filter((r) => r.status === ReportStatus.MATCHED).length,
          closed: mine.filter((r) => r.status === ReportStatus.CLOSED).length,
        });
        setMyRecent(mine.slice(0, 6));
        setMyReportIds(new Set(mine.map((r) => r.id)));
        if (!recent.reliable) {
          setMineError(tr({
            ar: "تعذّر التحقق من بلاغاتك حاليًا. حاول تسجيل الخروج والدخول مرة أخرى.",
            en: "Couldn't verify your reports right now. Try logging out and back in.",
            ur: "آپ کی رپورٹس کی تصدیق نہیں ہو سکی۔ لاگ آؤٹ کر کے دوبارہ لاگ ان کریں۔",
          }));
        }
      })
      .catch(() => !cancelled && setMineError(tr({
        ar: "تعذّر تحميل بلاغاتك.",
        en: "Couldn't load your reports.",
        ur: "آپ کی رپورٹس لوڈ نہیں ہو سکیں۔",
      })))
      .finally(() => !cancelled && setLoadingMine(false));

    return () => {
      cancelled = true;
    };
  }, [userId, tr]);

  useEffect(() => {
    let cancelled = false;

    if (!userId || myReportIds.size === 0) {
      Promise.resolve().then(() => !cancelled && setLoadingMatches(false));
      return () => {
        cancelled = true;
      };
    }

    Promise.resolve().then(() => !cancelled && setLoadingMatches(true));

    listMatches({ maxResultCount: 100, sorting: "creationTime desc" })
      .then((res) => {
        if (cancelled) return;
        const mine = (res?.items ?? []).filter(
          (m) =>
            m.status === MatchStatus.PENDING &&
            (myReportIds.has(m.lostReportId) || myReportIds.has(m.foundReportId))
        );
        setMyMatches(mine);
      })
      .catch(() => !cancelled && setMyMatches([]))
      .finally(() => !cancelled && setLoadingMatches(false));

    return () => {
      cancelled = true;
    };
  }, [userId, myReportIds]);

  const myKpis = [
    { label: lang === "ar" ? "إجمالي بلاغاتي" : "My total reports", value: myCounts.total, Icon: TrendingUp },
    { label: lang === "ar" ? "قيد المعالجة" : "Open", value: myCounts.open, Icon: Clock },
    { label: lang === "ar" ? "متطابقة" : "Matched", value: myCounts.matched, Icon: Sparkles },
    { label: lang === "ar" ? "مغلقة" : "Closed", value: myCounts.closed, Icon: Package },
  ];

  return (
    <section className="py-14 lg:py-20">
      <div className="max-w-7xl mx-auto px-6">
        <div className="flex items-end justify-between mb-10 flex-wrap gap-4">
          <div>
            <div className="text-[10px] font-mono uppercase tracking-widest text-primary mb-3">
              Luqya · {lang === "ar" ? "لوحتي" : "My dashboard"}
            </div>

            <h1 className="font-display text-4xl lg:text-5xl font-extrabold tracking-tight">
              {t("dashTitle")}
            </h1>

            <p className="text-muted-foreground text-lg mt-2">{t("dashSub")}</p>
          </div>

          <div className="inline-flex items-center gap-2 text-xs font-mono uppercase tracking-widest text-muted-foreground bg-card border border-border px-4 py-2 rounded-full">
            <span className="size-2 rounded-full bg-primary animate-pulse" />
            {profile ? (lang === "ar" ? "متصل" : "Live") : lang === "ar" ? "زائر" : "Guest"}
          </div>
        </div>

        {/* ---------- My reports — filtered client-side by creatorId ---------- */}
        <div className="mb-12">
          <div className="flex items-center gap-2 mb-5">
            <UserCircle className="size-4 text-primary" />
            <h2 className="font-display text-xl font-bold">
              {lang === "ar" ? "بلاغاتي" : "My reports"}
            </h2>
          </div>

          {!profile ? (
            <div className="p-6 rounded-2xl border border-border bg-card text-sm text-muted-foreground">
              {lang === "ar" ? "سجّل الدخول لعرض بلاغاتك." : "Log in to see your own reports."}
            </div>
          ) : loadingMine ? (
            <div className="flex items-center gap-2 text-muted-foreground py-6">
              <Loader2 className="size-4 animate-spin" />
              {lang === "ar" ? "جارٍ التحميل..." : "Loading..."}
            </div>
          ) : mineError ? (
            <div className="flex items-center gap-2.5 rounded-2xl bg-error-tint text-error px-4 py-3 text-sm">
              <AlertCircle className="size-4 shrink-0" />
              {mineError}
            </div>
          ) : (
            <>
              <div className="grid sm:grid-cols-2 lg:grid-cols-4 gap-4 mb-6">
                {myKpis.map(({ label, value, Icon }) => (
                  <div key={label} className="p-5 rounded-2xl bg-card border border-border">
                    <div className="flex items-center justify-between mb-3">
                      <span className="text-[10px] font-mono uppercase tracking-widest text-muted-foreground">
                        {label}
                      </span>
                      <Icon className="size-4 text-primary" />
                    </div>
                    <div className="font-display text-3xl font-extrabold tracking-tight">
                      {value.toLocaleString(lang)}
                    </div>
                  </div>
                ))}
              </div>

              {myRecent.length === 0 ? (
                <div className="p-6 rounded-2xl border border-border bg-card text-sm text-muted-foreground">
                  {lang === "ar" ? "لا توجد بلاغات مرتبطة بحسابك بعد." : "No reports linked to your account yet."}
                </div>
              ) : (
                <div className="grid sm:grid-cols-2 lg:grid-cols-3 gap-4">
                  {myRecent.map((r) => (
                    <Link
                      key={r.id}
                      to={`/match/${r.id}`}
                      className="p-5 rounded-2xl bg-card border border-border hover:border-primary/40 transition-colors"
                    >
                      <div className="flex items-center justify-between mb-3">
                        <span className="text-[10px] font-mono uppercase tracking-widest text-primary">
                          {r.type === 0 ? t("lost") : t("found")}
                        </span>
                        <span className="text-[10px] font-mono uppercase tracking-widest text-muted-foreground">
                          {t(reportStatusLabelKey(r.status))}
                        </span>
                      </div>
                      <p className="text-sm line-clamp-2">{(r.description ?? "").split(" — ").pop()}</p>
                    </Link>
                  ))}
                </div>
              )}
            </>
          )}
        </div>

        {/* ---------- Matches on my own reports — cross-referenced client-side
             against real creatorId-verified report ids (MatchAppService has
             no owner filter server-side). Only ever shows matches touching
             a report the current user actually created. ---------- */}
        {profile && myReportIds.size > 0 && (
          <div className="mb-12">
            <div className="flex items-center gap-2 mb-5">
              <Sparkles className="size-4 text-primary" />
              <h2 className="font-display text-xl font-bold">
                {lang === "ar" ? "مطابقات تحتاج ردك" : "Matches needing your response"}
              </h2>
            </div>

            {loadingMatches ? (
              <div className="flex items-center gap-2 text-muted-foreground py-6">
                <Loader2 className="size-4 animate-spin" />
                {lang === "ar" ? "جارٍ التحميل..." : "Loading..."}
              </div>
            ) : myMatches.length === 0 ? (
              <div className="p-6 rounded-2xl border border-border bg-card text-sm text-muted-foreground">
                {lang === "ar"
                  ? "لا توجد مطابقات تنتظر ردك حاليًا."
                  : "No matches waiting on your response right now."}
              </div>
            ) : (
              <div className="grid sm:grid-cols-2 gap-4">
                {myMatches.map((m) => {
                  // Always link to the *other* report in the pair — the one
                  // the user hasn't seen yet — never their own report id.
                  const otherReportId = myReportIds.has(m.lostReportId) ? m.foundReportId : m.lostReportId;
                  return (
                    <Link
                      key={m.id}
                      to={`/match/${otherReportId}`}
                      className="group flex items-center justify-between gap-4 p-5 rounded-2xl bg-card border border-border hover:border-primary/40 hover:-translate-y-0.5 transition-all"
                    >
                      <div className="min-w-0">
                        <span className="text-[10px] font-mono uppercase tracking-widest text-primary">
                          {lang === "ar" ? "احتمال تطابق" : "Possible match"}
                        </span>
                        <p className="text-sm text-muted-foreground mt-1">
                          {lang === "ar" ? "افتح البلاغ لمعرفة إن كان يخصّك." : "Open the report to see if it's yours."}
                        </p>
                      </div>
                      <div className="flex items-center gap-3 shrink-0">
                        {typeof m.similarityScore === "number" && (
                          <span className="text-sm font-bold font-mono text-primary">
                            {Math.round(m.similarityScore)}%
                          </span>
                        )}
                        <span className="grid size-8 place-items-center rounded-full bg-primary/5 text-primary group-hover:bg-primary group-hover:text-primary-foreground transition-colors">
                          <ArrowRight className={`size-4 ${lang === "ar" ? "rotate-180" : ""}`} />
                        </span>
                      </div>
                    </Link>
                  );
                })}
              </div>
            )}
          </div>
        )}

        {/* ---------- Quick actions — real navigation only, no fabricated
             metrics to fill space when a user's history is still thin. ---------- */}
        <div>
          <div className="flex items-center gap-2 mb-5">
            <Compass className="size-4 text-primary" />
            <h2 className="font-display text-xl font-bold">
              {lang === "ar" ? "إجراءات سريعة" : "Quick actions"}
            </h2>
          </div>

          <div className="grid sm:grid-cols-3 gap-4">
            <Link
              to="/report"
              className="group p-6 rounded-2xl bg-card border border-border hover:border-primary/40 transition-colors"
            >
              <PlusCircle className="size-5 text-primary mb-3" />
              <div className="font-semibold mb-1 group-hover:text-primary transition-colors">
                {lang === "ar" ? "إنشاء بلاغ جديد" : "Create a new report"}
              </div>
              <p className="text-xs text-muted-foreground">
                {lang === "ar" ? "عن مفقود أو موجود." : "For something lost or found."}
              </p>
            </Link>

            <Link
              to="/search"
              className="group p-6 rounded-2xl bg-card border border-border hover:border-primary/40 transition-colors"
            >
              <Sparkles className="size-5 text-primary mb-3" />
              <div className="font-semibold mb-1 group-hover:text-primary transition-colors">
                {lang === "ar" ? "بحث ذكي" : "Smart search"}
              </div>
              <p className="text-xs text-muted-foreground">
                {lang === "ar" ? "ابحث بالوصف عن مطابقات محتملة." : "Search existing reports by description."}
              </p>
            </Link>

            <Link
              to="/browse"
              className="group p-6 rounded-2xl bg-card border border-border hover:border-primary/40 transition-colors"
            >
              <Search className="size-5 text-primary mb-3" />
              <div className="font-semibold mb-1 group-hover:text-primary transition-colors">
                {lang === "ar" ? "تصفح البلاغات" : "Browse reports"}
              </div>
              <p className="text-xs text-muted-foreground">
                {lang === "ar" ? "استعرض كل البلاغات المتاحة." : "See everything reported so far."}
              </p>
            </Link>
          </div>
        </div>
      </div>
    </section>
  );
}
