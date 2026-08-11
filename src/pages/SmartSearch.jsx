import { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import { Search, Loader2, AlertCircle, MapPin, Sparkles, RotateCcw } from "lucide-react";

import { useI18n } from "../lib/useI18n";
import { useAuth } from "../lib/useAuth";
import DammaMark from "../components/DammaMark";
import { aiSearch } from "../api/search";
import { ReportType } from "../api/enums";
import { fetchMyReports } from "../lib/myReports";

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
  const { userId } = useAuth();
  const [text, setText] = useState("");
  const [typeFilter, setTypeFilter] = useState("found");
  const [status, setStatus] = useState("idle"); // idle | loading | success | empty | error
  const [results, setResults] = useState([]);
  const [errorMsg, setErrorMsg] = useState(null);
  const [ownReportsExcluded, setOwnReportsExcluded] = useState(0);

  useEffect(() => {
    document.title = tr({ ar: "البحث الذكي — لُقيا", en: "Smart search — Luqya", ur: "ذہین تلاش — لقیا" });
  }, [tr]);

  async function runSearch(event) {
    event?.preventDefault();
    if (!text.trim()) return;

    setStatus("loading");
    setErrorMsg(null);
    setOwnReportsExcluded(0);

    try {
      // Only send `type` when a specific one is selected — AiSearchInputDto.Type
      // is nullable on the backend specifically so omitting it searches both.
      const selected = TYPE_FILTERS.find((f) => f.key === typeFilter);
      const data = await aiSearch({
        text: text.trim(),
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
              disabled={status === "loading" || !text.trim()}
              className="inline-flex items-center gap-2 bg-primary text-primary-foreground px-6 py-3 rounded-2xl font-semibold shadow-glow hover:-translate-y-0.5 transition-transform disabled:opacity-60 disabled:translate-y-0"
            >
              {status === "loading" ? <Loader2 className="size-4 animate-spin" /> : <Search className="size-4" />}
              {status === "loading" ? t("searchLoading") : t("searchBtn")}
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
            <div className="text-center py-16 text-muted-foreground">{t("searchEmpty")}</div>
          )}

          {status === "success" && (
            <>
              {ownReportsExcluded > 0 && (
                <p className="text-xs text-muted-foreground mb-4">{t("searchOwnExcludedNote")}</p>
              )}
              <div className="grid sm:grid-cols-2 lg:grid-cols-3 gap-5 animate-rise-in">
                {results.map((r) => (
                  <Link
                    key={r.reportId}
                    to={`/match/${r.reportId}?source=smart-search`}
                    className="group rounded-[1.75rem] border border-border bg-card overflow-hidden shadow-soft hover:shadow-luxe hover:-translate-y-1 transition-all"
                  >
                    <div className="aspect-[16/10] bg-gradient-to-br from-stone-100 to-stone-200 relative grid place-items-center">
                      <Sparkles className="size-8 text-primary/40" />
                      {typeof r.scorePercentage === "number" && (
                        <span className="absolute top-3 end-3 bg-primary text-primary-foreground text-xs font-bold font-mono px-2.5 py-1 rounded-full">
                          {Math.round(r.scorePercentage)}%
                        </span>
                      )}
                    </div>
                    <div className="p-5">
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
                ))}
              </div>
            </>
          )}
        </div>
      </div>
    </section>
  );
}
