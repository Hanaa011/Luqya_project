import { useEffect, useState } from "react";
import { Link, useParams } from "react-router-dom";
import {
  ArrowLeft,
  MapPin,
  Clock,
  Sparkles,
  Check,
  X,
  MessageCircle,
  BrainCircuit,
  Loader2,
  AlertCircle,
} from "lucide-react";

import { useI18n } from "../lib/useI18n";
import { useAuth } from "../lib/useAuth";
import { getReport } from "../api/reports";
import { listMatches, acceptMatch, rejectMatch } from "../api/matches";
import { createNotification } from "../api/notifications";
import { ReportType, reportStatusLabelKey } from "../api/enums";
import { ApiError } from "../api/httpClient";

export default function Match() {
  const { id } = useParams();
  const { t, lang, tr } = useI18n();
  const { profile } = useAuth();

  const [report, setReport] = useState(null);
  const [match, setMatch] = useState(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);
  const [actionState, setActionState] = useState("idle"); // idle | working | done

  useEffect(() => {
    document.title = "AI match — Luqya";
  }, []);

  useEffect(() => {
    let cancelled = false;

    Promise.resolve().then(() => {
      if (cancelled) return;
      setLoading(true);
      setError(null);
    });

    Promise.all([
      getReport(id),
      // NOTE: MatchAsync has no reportId filter in ForgeService, so the
      // only way to find "the match for this report" today is to page
      // through the list client-side. Worth asking the backend for a
      // filter param (e.g. reportId) to replace this.
      listMatches({ maxResultCount: 200 }).catch(() => ({ items: [] })),
    ])
      .then(([reportData, matchesData]) => {
        if (cancelled) return;
        setReport(reportData);
        const found = (matchesData?.items ?? []).find(
          (m) => m.lostReportId === reportData.id || m.foundReportId === reportData.id
        );
        setMatch(found ?? null);
      })
      .catch((err) => {
        if (cancelled) return;
        setError(
          err instanceof ApiError && err.status === 404
            ? tr({ ar: "لم يُعثر على هذا البلاغ.", en: "This report couldn't be found.", ur: "یہ رپورٹ نہیں مل سکی۔" })
            : tr({ ar: "تعذّر تحميل التفاصيل.", en: "Couldn't load the details.", ur: "تفصیلات لوڈ نہیں ہو سکیں۔" })
        );
      })
      .finally(() => !cancelled && setLoading(false));

    return () => {
      cancelled = true;
    };
  }, [id, tr]);

  async function handleAccept() {
    if (!match) return;
    setActionState("working");
    try {
      const updated = await acceptMatch(match.id);
      setMatch(updated);
      setActionState("done");
    } catch {
      setActionState("idle");
    }
  }

  async function handleReject() {
    if (!match) return;
    setActionState("working");
    try {
      const updated = await rejectMatch(match.id);
      setMatch(updated);
      setActionState("idle");
    } catch {
      setActionState("idle");
    }
  }

  async function handleContact() {
    if (!report) return;
    setActionState("working");
    try {
      await createNotification({
        reporterId: report.reporterId,
        reportId: report.id,
        title: tr({ ar: "رسالة تواصل", en: "Contact request", ur: "رابطہ کی درخواست" }),
        message: tr({
          ar: "أبدى أحد المستخدمين اهتمامًا ببلاغك — راجع لوحتك للتفاصيل.",
          en: "Someone is interested in your report — check your dashboard for details.",
          ur: "کسی نے آپ کی رپورٹ میں دلچسپی ظاہر کی ہے — تفصیلات کے لیے ڈیش بورڈ دیکھیں۔",
        }),
      });
    } finally {
      setActionState("idle");
    }
  }

  if (loading) {
    return (
      <div className="flex items-center justify-center gap-2 py-32 text-muted-foreground">
        <Loader2 className="size-5 animate-spin" />
        {lang === "ar" ? "جارٍ التحميل..." : "Loading..."}
      </div>
    );
  }

  if (error || !report) {
    return (
      <div className="flex flex-col items-center justify-center gap-3 py-32 text-error text-center px-6">
        <AlertCircle className="size-6" />
        <p>{error}</p>
        <Link to="/browse" className="text-primary font-semibold hover:underline text-sm mt-2">
          {lang === "ar" ? "عودة إلى التصفح" : "Back to browse"}
        </Link>
      </div>
    );
  }

  const [maybeTitle, ...rest] = (report.description ?? "").split(" — ");
  const title = rest.length ? maybeTitle : report.aiObjectType || t("browseTitle");
  const desc = rest.length ? rest.join(" — ") : maybeTitle;

  const aiTags = [report.aiObjectType, report.aiBrand, report.color, ...(report.aiTags ?? [])].filter(Boolean);
  const score = match?.similarityScore != null ? Math.round(match.similarityScore) : null;

  return (
    <section className="py-16 lg:py-24">
      <div className="max-w-6xl mx-auto px-6">
        <Link
          to="/browse"
          className="inline-flex items-center gap-2 text-sm font-semibold text-muted-foreground hover:text-primary mb-8 transition-colors"
        >
          <ArrowLeft className={`size-4 ${lang === "ar" ? "rotate-180" : ""}`} />
          {lang === "ar" ? "عودة إلى التصفح" : "Back to browse"}
        </Link>

        <div className="grid lg:grid-cols-[1.4fr_1fr] gap-8">
          <div className="bg-card border border-border rounded-[2rem] overflow-hidden shadow-soft">
            <div className="aspect-[16/10] bg-gradient-to-br from-stone-100 to-stone-200 relative grid place-items-center">
              {report.imagePath ? (
                <img src={report.imagePath} alt="" className="absolute inset-0 size-full object-cover" />
              ) : (
                <div className="size-24 rounded-3xl bg-card shadow-luxe grid place-items-center">
                  <BrainCircuit className="size-10 text-primary" strokeWidth={1.5} />
                </div>
              )}

              <span className="absolute bottom-4 end-4 text-[10px] font-mono uppercase tracking-widest text-muted-foreground/60 bg-card/80 px-2 py-1 rounded-full">
                Report #{String(report.id).slice(0, 8)}
              </span>
            </div>

            <div className="p-8 lg:p-10">
              <div className="flex items-center justify-between mb-4">
                <span className="text-[10px] font-mono uppercase tracking-widest bg-primary/5 text-primary px-2.5 py-1 rounded-full">
                  {report.type === ReportType.LOST ? t("lost") : t("found")} · {t(reportStatusLabelKey(report.status))}
                </span>

                <div className="flex items-center gap-4 text-xs text-muted-foreground">
                  <span className="inline-flex items-center gap-1.5">
                    <MapPin className="size-3" />
                    {report.locationDetails || "—"}
                  </span>

                  <span className="inline-flex items-center gap-1.5">
                    <Clock className="size-3" />
                    {new Date(report.creationTime).toLocaleDateString(lang)}
                  </span>
                </div>
              </div>

              <h1 className="font-display text-3xl lg:text-4xl font-extrabold tracking-tight mb-4">
                {title}
              </h1>

              <p className="text-muted-foreground leading-relaxed mb-8">{desc}</p>

              <div className="border-t border-border pt-8">
                <div className="text-[10px] font-mono uppercase tracking-widest text-primary font-bold mb-4">
                  {lang === "ar" ? "لماذا قد يكون هذا تطابقًا" : "Why this might be a match"}
                </div>

                {match?.matchReason ? (
                  <p className="text-sm text-foreground/80 leading-relaxed">{match.matchReason}</p>
                ) : aiTags.length > 0 ? (
                  <ul className="space-y-3 text-sm">
                    {aiTags.map((tag, index) => (
                      <li key={index} className="flex items-start gap-3">
                        <span className="mt-1 size-5 rounded-full bg-primary/5 text-primary grid place-items-center shrink-0">
                          <Check className="size-3" />
                        </span>
                        <span className="text-foreground/80 leading-relaxed">{tag}</span>
                      </li>
                    ))}
                  </ul>
                ) : (
                  <p className="text-sm text-muted-foreground">
                    {lang === "ar" ? "لم يُصنَّف الغرض بعد." : "This item hasn't been classified yet."}
                  </p>
                )}
              </div>
            </div>
          </div>

          <div className="space-y-5">
            <div className="p-8 rounded-[2rem] bg-primary text-primary-foreground shadow-luxe relative overflow-hidden">
              <div className="absolute -top-16 -end-16 size-64 rounded-full bg-accent/20 blur-3xl" />

              <div className="relative">
                <div className="text-[10px] font-mono uppercase tracking-widest text-white/60 mb-2">
                  {lang === "ar" ? "درجة الثقة" : "Confidence score"}
                </div>

                {score != null ? (
                  <>
                    <div className="font-display text-7xl font-extrabold mb-4 tracking-tight">{score}%</div>
                    <div className="h-1.5 rounded-full bg-white/10 overflow-hidden mb-2">
                      <div className="h-full bg-accent rounded-full" style={{ width: `${score}%` }} />
                    </div>
                    <div className="text-xs text-white/70">
                      {score >= 90
                        ? lang === "ar"
                          ? "تطابق مرتفع جدًا"
                          : "Very high match probability"
                        : lang === "ar"
                        ? "تطابق محتمل"
                        : "Possible match"}
                    </div>
                  </>
                ) : (
                  <div className="text-sm text-white/70 py-4">
                    {lang === "ar"
                      ? "لا يوجد تطابق مؤكد لهذا البلاغ بعد."
                      : "No confirmed match for this report yet."}
                  </div>
                )}
              </div>
            </div>

            <div className="p-6 rounded-[1.5rem] bg-card border border-border">
              <div className="text-[10px] font-mono uppercase tracking-widest text-muted-foreground mb-4">
                {lang === "ar" ? "إجراءات" : "Actions"}
              </div>

              <div className="space-y-2.5">
                {profile && (
                  <button
                    type="button"
                    onClick={handleAccept}
                    disabled={!match || actionState === "working"}
                    className="w-full inline-flex items-center justify-center gap-2 bg-primary text-primary-foreground px-5 py-3.5 rounded-2xl font-semibold text-sm hover:-translate-y-0.5 transition-transform disabled:opacity-50 disabled:translate-y-0"
                  >
                    {actionState === "working" ? <Loader2 className="size-4 animate-spin" /> : <Check className="size-4" />}
                    {lang === "ar" ? "هذا يخصّني" : "This is mine"}
                  </button>
                )}

                <button
                  type="button"
                  onClick={handleContact}
                  disabled={actionState === "working"}
                  className="w-full inline-flex items-center justify-center gap-2 bg-card border border-border px-5 py-3.5 rounded-2xl font-semibold text-sm hover:bg-stone-100 transition-colors disabled:opacity-50"
                >
                  <MessageCircle className="size-4" />
                  {lang === "ar" ? "تواصل" : "Contact"}
                </button>

                {profile && (
                  <button
                    type="button"
                    onClick={handleReject}
                    disabled={!match || actionState === "working"}
                    className="w-full inline-flex items-center justify-center gap-2 text-muted-foreground px-5 py-3 rounded-2xl font-semibold text-sm hover:text-foreground transition-colors disabled:opacity-40"
                  >
                    <X className="size-4" />
                    {lang === "ar" ? "ليس لي" : "Not mine"}
                  </button>
                )}

                {!profile && (
                  <p className="text-xs text-muted-foreground text-center pt-1">
                    {lang === "ar" ? "سجّل الدخول لإدارة هذا التطابق." : "Log in to manage this match."}
                  </p>
                )}
              </div>
            </div>

            <div className="p-6 rounded-[1.5rem] bg-stone-100/60 border border-border">
              <div className="text-[10px] font-mono uppercase tracking-widest text-muted-foreground mb-3">
                {lang === "ar" ? "الجدول الزمني" : "Timeline"}
              </div>

              <ol className="space-y-3 text-sm">
                <li className="flex items-center gap-3">
                  <Sparkles className="size-3.5 text-primary" />
                  <span className="flex-1">{lang === "ar" ? "قُدّم البلاغ" : "Report submitted"}</span>
                  <span className="text-[10px] font-mono uppercase text-muted-foreground">
                    {new Date(report.creationTime).toLocaleDateString(lang)}
                  </span>
                </li>

                {match && (
                  <li className="flex items-center gap-3">
                    <Sparkles className="size-3.5 text-primary" />
                    <span className="flex-1">{lang === "ar" ? "تم العثور على تطابق" : "Match found"}</span>
                    <span className="text-[10px] font-mono uppercase text-muted-foreground">
                      {new Date(match.creationTime).toLocaleDateString(lang)}
                    </span>
                  </li>
                )}
              </ol>
            </div>
          </div>
        </div>
      </div>
    </section>
  );
}
