import { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import { Search, MapPin, Clock, Loader2, AlertCircle, LogIn } from "lucide-react";

import { useI18n } from "../lib/useI18n";
import { useAuth } from "../lib/useAuth";
import { fetchMyReports } from "../lib/myReports";
import { ReportType, reportStatusLabelKey } from "../api/enums";

export default function Browse() {
  const { t, lang, tr } = useI18n();
  const { profile, userId, loading: authLoading } = useAuth();
  const [q, setQ] = useState("");
  const [filter, setFilter] = useState("all");
  const [allRows, setAllRows] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);

  useEffect(() => {
    document.title = "Browse reports — Luqya";
  }, []);

  useEffect(() => {
    // Browse is a personal "my reports" view, not a public report
    // browser — guests never get report cards, and never trigger the
    // request at all, so no report data is ever fetched for them.
    let cancelled = false;

    if (authLoading || !userId) {
      Promise.resolve().then(() => {
        if (cancelled) return;
        setAllRows([]);
        setLoading(false);
      });
      return () => {
        cancelled = true;
      };
    }

    Promise.resolve().then(() => {
      if (cancelled) return;
      setLoading(true);
      setError(null);
    });

    // Same single source of truth as Dashboard — ownership via
    // Report.CreatorId, filtered client-side (see lib/myReports.js). Any
    // q/type filtering below happens client-side, on top of an already
    // ownership-verified list.
    fetchMyReports({ userId, maxResultCount: 500, signal: undefined })
      .then((res) => {
        if (cancelled) return;
        setAllRows(res.reliable ? res.reports : []);
        if (!res.reliable) {
          setError(
            tr({
              ar: "تعذّر تحميل البلاغات. حاول مرة أخرى.",
              en: "Couldn't load reports. Please try again.",
              ur: "رپورٹس لوڈ نہیں ہو سکیں۔ دوبارہ کوشش کریں۔",
            })
          );
        }
      })
      .catch(() => {
        if (!cancelled) {
          setError(
            tr({
              ar: "تعذّر تحميل البلاغات. حاول مرة أخرى.",
              en: "Couldn't load reports. Please try again.",
              ur: "رپورٹس لوڈ نہیں ہو سکیں۔ دوبارہ کوشش کریں۔",
            })
          );
        }
      })
      .finally(() => !cancelled && setLoading(false));

    return () => {
      cancelled = true;
    };
  }, [tr, authLoading, userId]);

  const rows = allRows.filter((report) => {
    if (filter !== "all") {
      const wantType = filter === "lost" ? ReportType.LOST : ReportType.FOUND;
      if (report.type !== wantType) return false;
    }
    if (q.trim()) {
      const needle = q.trim().toLowerCase();
      const haystack = `${report.description ?? ""} ${report.locationDetails ?? ""}`.toLowerCase();
      if (!haystack.includes(needle)) return false;
    }
    return true;
  });

  return (
    <section className="py-16 lg:py-24">
      <div className="max-w-7xl mx-auto px-6">
        <h1 className="font-display text-4xl lg:text-5xl font-extrabold tracking-tight mb-3">
          {t("browseTitle")}
        </h1>

        <p className="text-muted-foreground text-lg mb-10">
          {lang === "ar" ? "بلاغاتك الشخصية في مكان واحد." : "Your own reports, all in one place."}
        </p>

        {!authLoading && !profile && (
          <div className="flex flex-col items-center text-center gap-4 py-16 sm:py-24 border border-dashed border-border rounded-[2rem]">
            <span className="grid size-14 place-items-center rounded-full bg-primary/5 text-primary">
              <LogIn className="size-6" />
            </span>
            <p className="text-lg font-semibold">
              {lang === "ar" ? "سجّل الدخول لعرض بلاغاتك" : "Log in to see your reports"}
            </p>
            <p className="text-sm text-muted-foreground max-w-sm">
              {lang === "ar"
                ? "هذه الصفحة تعرض بلاغاتك الشخصية فقط."
                : "This page shows your own reports only."}
            </p>
            <Link
              to="/auth/login"
              className="inline-flex items-center gap-2 bg-primary text-primary-foreground px-6 py-3 rounded-2xl font-semibold text-sm shadow-glow hover:-translate-y-0.5 transition-transform"
            >
              {t("navLogin")}
            </Link>
          </div>
        )}

        {(authLoading || profile) && (
          <>
            <div className="flex flex-col md:flex-row gap-3 mb-10">
              <div className="relative flex-1">
                <Search className="absolute top-1/2 -translate-y-1/2 start-5 size-4 text-muted-foreground" />

                <input
                  type="text"
                  value={q}
                  onChange={(event) => setQ(event.target.value)}
                  placeholder={t("browseSearchPh")}
                  className="w-full ps-12 pe-5 py-4 rounded-2xl bg-card border border-border focus:border-primary focus:outline-none focus:ring-2 focus:ring-primary/10 transition-all"
                />
              </div>

              <div className="inline-flex p-1.5 bg-stone-100 rounded-2xl gap-1 self-start">
                {["all", "lost", "found"].map((item) => (
                  <button
                    type="button"
                    key={item}
                    onClick={() => setFilter(item)}
                    className={`px-5 py-2.5 rounded-xl text-sm font-semibold transition-all ${
                      filter === item
                        ? "bg-card text-primary shadow-soft"
                        : "text-muted-foreground hover:text-foreground"
                    }`}
                  >
                    {item === "all"
                      ? lang === "ar"
                        ? "الكل"
                        : "All"
                      : t(item)}
                  </button>
                ))}
              </div>
            </div>

            {(loading || authLoading) && (
              <div className="flex items-center justify-center gap-2 py-20 text-muted-foreground">
                <Loader2 className="size-5 animate-spin" />
                {lang === "ar" ? "جارٍ التحميل..." : "Loading..."}
              </div>
            )}

            {!loading && !authLoading && error && (
              <div className="flex items-center justify-center gap-2.5 py-20 text-error">
                <AlertCircle className="size-5" />
                {error}
              </div>
            )}

            {!loading && !authLoading && !error && (
              <div className="grid md:grid-cols-2 lg:grid-cols-3 gap-5">
                {rows.map((report) => {
                  const [maybeTitle, ...rest] = (report.description ?? "").split(" — ");
                  const title = rest.length ? maybeTitle : null;
                  const desc = rest.length ? rest.join(" — ") : maybeTitle;

                  return (
                    <Link
                      key={report.id}
                      to={`/match/${report.id}`}
                      className="group p-7 rounded-3xl bg-card border border-border hover:shadow-luxe hover:-translate-y-1 transition-all duration-500"
                    >
                      <div className="flex items-center justify-between mb-5">
                        <span
                          className={`text-[10px] font-mono uppercase tracking-widest px-2.5 py-1 rounded-full ${
                            report.type === ReportType.LOST
                              ? "bg-primary/5 text-primary"
                              : "bg-accent/10 text-accent-foreground"
                          }`}
                        >
                          {report.type === ReportType.LOST ? t("lost") : t("found")}
                        </span>

                        <span className="text-[10px] font-mono uppercase tracking-widest text-muted-foreground">
                          {t(reportStatusLabelKey(report.status))}
                        </span>
                      </div>

                      <h3 className="font-display text-xl font-bold mb-2">
                        {title || report.aiObjectType || t("browseTitle")}
                      </h3>

                      <p className="text-sm text-muted-foreground mb-6 line-clamp-2 leading-relaxed">
                        {desc}
                      </p>

                      <div className="flex items-center justify-between text-xs text-muted-foreground pt-4 border-t border-border">
                        <span className="inline-flex items-center gap-1.5">
                          <MapPin className="size-3" />
                          {report.locationDetails || "—"}
                        </span>

                        <span className="inline-flex items-center gap-1.5">
                          <Clock className="size-3" />
                          {new Date(report.creationTime).toLocaleDateString(lang)}
                        </span>
                      </div>
                    </Link>
                  );
                })}
              </div>
            )}

            {!loading && !authLoading && !error && rows.length === 0 && (
              <div className="text-center py-20 text-muted-foreground">
                {lang === "ar" ? "لا توجد بلاغات لعرضها بعد." : "No reports to show yet."}
              </div>
            )}
          </>
        )}
      </div>
    </section>
  );
}
