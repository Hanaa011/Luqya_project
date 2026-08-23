import { useEffect, useState } from "react";
import { Link, useParams } from "react-router-dom";
import {
  ArrowLeft,
  AlertCircle,
  AtSign,
  Loader2,
  Mail,
  MessageCircle,
  Phone,
  ShieldCheck,
  UserRound,
} from "lucide-react";

import { useI18n } from "../lib/useI18n";
import { getReport } from "../api/reports";
import { getReporter } from "../api/reporters";
import { PreferredContactType, ReportType, preferredContactLabelKey } from "../api/enums";
import { ApiError } from "../api/httpClient";
import { reportHeadingTitle } from "../lib/reportTitle";

function copy(lang, values) {
  return values[lang] ?? values.en;
}

// Phase 4 Part 6 (Task C): a short, extracted heading, not the full
// description - matches every other page's report heading now.
function reportSummary(report, fallback) {
  return reportHeadingTitle(report, fallback);
}

export default function Contact() {
  const { id } = useParams();
  const { t, lang } = useI18n();

  const [report, setReport] = useState(null);
  const [reporter, setReporter] = useState(null);
  const [loading, setLoading] = useState(true);
  const [errorCode, setErrorCode] = useState(null);

  useEffect(() => {
    document.title = "Contact reporter — Luqya";
  }, []);

  useEffect(() => {
    let cancelled = false;
    const controller = new AbortController();

    Promise.resolve().then(() => {
      if (cancelled) return;
      setLoading(true);
      setErrorCode(null);
    });

    getReport(id, controller.signal)
      .then(async (reportData) => {
        if (cancelled) return;
        setReport(reportData);
        const reporterData = await getReporter(reportData.reporterId, controller.signal);
        if (!cancelled) setReporter(reporterData);
      })
      .catch((error) => {
        if (cancelled) return;
        if (error instanceof ApiError && error.status === 404) {
          setErrorCode("not-found");
        } else if (error instanceof ApiError && error.status === 403) {
          setErrorCode("forbidden");
        } else {
          setErrorCode("generic");
        }
      })
      .finally(() => !cancelled && setLoading(false));

    return () => {
      cancelled = true;
      controller.abort();
    };
  }, [id]);

  if (loading) {
    return (
      <div className="flex items-center justify-center gap-2 py-32 text-muted-foreground">
        <Loader2 className="size-5 animate-spin" />
        {copy(lang, { ar: "جارٍ تحميل معلومات التواصل...", en: "Loading contact details...", ur: "رابطے کی معلومات لوڈ ہو رہی ہیں..." })}
      </div>
    );
  }

  if (errorCode || !report || !reporter) {
    const message =
      errorCode === "forbidden"
        ? copy(lang, {
            ar: "معلومات التواصل غير متاحة لهذا الحساب.",
            en: "Contact details aren't available to this account.",
            ur: "اس اکاؤنٹ کے لیے رابطے کی معلومات دستیاب نہیں ہیں۔",
          })
        : errorCode === "not-found"
          ? copy(lang, {
              ar: "تعذّر العثور على بيانات صاحب البلاغ.",
              en: "The report owner couldn't be found.",
              ur: "رپورٹ بنانے والے کی معلومات نہیں مل سکیں۔",
            })
          : copy(lang, {
              ar: "تعذّر تحميل معلومات التواصل.",
              en: "Couldn't load contact details.",
              ur: "رابطے کی معلومات لوڈ نہیں ہو سکیں۔",
            });

    return (
      <div className="flex flex-col items-center justify-center gap-3 py-32 px-6 text-center">
        <AlertCircle className="size-7 text-error" />
        <p className="max-w-md text-sm text-muted-foreground">{message}</p>
        <Link to={`/match/${id}`} className="mt-2 text-sm font-semibold text-primary hover:underline">
          {copy(lang, { ar: "العودة إلى البلاغ", en: "Back to report", ur: "رپورٹ پر واپس جائیں" })}
        </Link>
      </div>
    );
  }

  const isLost = report.type === ReportType.LOST;
  const name = reporter.name?.trim() || copy(lang, { ar: "الاسم غير متاح", en: "Name unavailable", ur: "نام دستیاب نہیں" });
  const phone = reporter.phone?.trim();
  const email = reporter.email?.trim();
  const preferredLabel = t(preferredContactLabelKey(reporter.preferredContact));

  return (
    <section className="py-10 sm:py-14 lg:py-20">
      <div className="mx-auto max-w-4xl px-4 sm:px-6">
        <Link
          to={`/match/${report.id}`}
          className="mb-7 inline-flex min-h-11 items-center gap-2 rounded-xl px-1 text-sm font-semibold text-muted-foreground transition-colors hover:text-primary focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
        >
          <ArrowLeft className={`size-4 ${lang === "ar" || lang === "ur" ? "rotate-180" : ""}`} />
          {copy(lang, { ar: "العودة إلى البلاغ", en: "Back to report", ur: "رپورٹ پر واپس جائیں" })}
        </Link>

        <div className="overflow-hidden rounded-[2rem] border border-border bg-card shadow-soft">
          <div className={`border-b border-border px-6 py-6 sm:px-8 ${isLost ? "bg-warn-tint/50" : "bg-success-tint/50"}`}>
            <div className="flex flex-wrap items-start justify-between gap-4">
              <div>
                <div className={`mb-3 inline-flex items-center gap-2 rounded-full px-3 py-1 text-xs font-bold ${isLost ? "bg-warn-tint text-warn" : "bg-success-tint text-success"}`}>
                  <span className="size-1.5 rounded-full bg-current" />
                  {isLost ? t("lost") : t("found")}
                </div>
                <h1 className="font-display text-2xl font-extrabold tracking-tight sm:text-3xl">
                  {copy(lang, { ar: "التواصل مع صاحب البلاغ", en: "Contact the report creator", ur: "رپورٹ بنانے والے سے رابطہ" })}
                </h1>
                <p className="mt-2 max-w-2xl text-sm leading-relaxed text-muted-foreground">
                  {copy(lang, {
                    ar: "هذه المعلومات تخص الشخص الذي أنشأ هذا البلاغ. استخدمها للتواصل بخصوص هذا الغرض فقط.",
                    en: "This person created this report. Use these details only to coordinate about this item.",
                    ur: "اس شخص نے یہ رپورٹ بنائی ہے۔ ان معلومات کو صرف اسی چیز کے بارے میں رابطے کے لیے استعمال کریں۔",
                  })}
                </p>
              </div>
              <ShieldCheck className="size-6 text-primary" />
            </div>
          </div>

          <div className="grid gap-8 p-6 sm:p-8 md:grid-cols-[0.9fr_1.1fr]">
            <div className="space-y-5">
              <div className="flex items-center gap-3">
                <div className="grid size-12 shrink-0 place-items-center rounded-2xl bg-primary/[0.08] text-primary">
                  <UserRound className="size-5" />
                </div>
                <div className="min-w-0">
                  <p className="text-xs text-muted-foreground">
                    {copy(lang, { ar: "صاحب البلاغ", en: "Report creator", ur: "رپورٹ بنانے والا" })}
                  </p>
                  <p className="truncate text-lg font-bold">{name}</p>
                </div>
              </div>

              <div className="rounded-2xl border border-border bg-stone-50 p-4">
                <p className="text-[11px] font-mono uppercase tracking-widest text-muted-foreground">
                  {copy(lang, { ar: "طريقة التواصل المفضلة", en: "Preferred contact", ur: "ترجیحی رابطہ" })}
                </p>
                <div className="mt-2 flex items-center gap-2 font-semibold">
                  <MessageCircle className="size-4 text-primary" />
                  {preferredLabel}
                </div>
              </div>

              <div className="rounded-2xl border border-border p-4">
                <p className="text-[11px] font-mono uppercase tracking-widest text-muted-foreground">
                  {copy(lang, { ar: "البلاغ", en: "Report", ur: "رپورٹ" })}
                </p>
                <p className="mt-2 text-sm font-semibold leading-relaxed">
                  {reportSummary(report, t("browseTitle"))}
                </p>
              </div>
            </div>

            <div className="space-y-3">
              <ContactRow
                Icon={Phone}
                label={t("contactPhone")}
                value={phone}
                href={phone ? `tel:${phone}` : null}
                unavailable={copy(lang, { ar: "غير متاح", en: "Not provided", ur: "دستیاب نہیں" })}
              />
              <ContactRow
                Icon={Mail}
                label={t("contactEmail")}
                value={email}
                href={email ? `mailto:${email}` : null}
                unavailable={copy(lang, { ar: "غير متاح", en: "Not provided", ur: "دستیاب نہیں" })}
              />

              {reporter.preferredContact === PreferredContactType.WHATSAPP && phone && (
                <div className="flex items-start gap-3 rounded-2xl bg-primary/5 px-4 py-3 text-sm text-muted-foreground">
                  <AtSign className="mt-0.5 size-4 shrink-0 text-primary" />
                  <span>
                    {copy(lang, {
                      ar: "يفضّل صاحب البلاغ التواصل عبر واتساب على رقم الهاتف أعلاه.",
                      en: "The report creator prefers WhatsApp using the phone number above.",
                      ur: "رپورٹ بنانے والا اوپر دیے گئے فون نمبر پر واٹس ایپ کو ترجیح دیتا ہے۔",
                    })}
                  </span>
                </div>
              )}
            </div>
          </div>
        </div>
      </div>
    </section>
  );
}

function ContactRow({ Icon, label, value, href, unavailable }) {
  const content = (
    <>
      <div className="grid size-10 shrink-0 place-items-center rounded-xl bg-stone-100 text-primary">
        <Icon className="size-4" />
      </div>
      <div className="min-w-0 flex-1">
        <p className="text-xs text-muted-foreground">{label}</p>
        <p className={`mt-0.5 truncate text-sm font-semibold ${value ? "text-foreground" : "text-muted-foreground"}`}>
          {value || unavailable}
        </p>
      </div>
    </>
  );

  if (!href) {
    return <div className="flex min-h-16 items-center gap-3 rounded-2xl border border-border px-4 py-3">{content}</div>;
  }

  return (
    <a
      href={href}
      className="flex min-h-16 items-center gap-3 rounded-2xl border border-border px-4 py-3 transition-colors hover:border-primary/35 hover:bg-primary/[0.025] focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
    >
      {content}
    </a>
  );
}
