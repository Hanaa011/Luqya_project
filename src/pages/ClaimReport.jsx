import { useEffect, useState } from "react";
import { useNavigate, useParams } from "react-router-dom";
import { CheckCircle2, AlertCircle, Loader2 } from "lucide-react";

import { useI18n } from "../lib/useI18n";
import { confirmReporterClaim } from "../api/reporters";

function copy(lang, values) {
  return values[lang] ?? values.en;
}

// Entry point for the link ConversationAppService.OpenAsync emails a guest
// report's original reporter (see ReporterManager.IssueClaimTokenIfNeededAsync
// / ClaimGuestReportAsync). Wrapped in RequireAuth by the route in App.jsx,
// so by the time this renders the visitor is already logged in/registered -
// this just redeems the token for them and reports the outcome. No contact
// info, no report details are shown here - only success/failure.
export default function ClaimReport() {
  const { token } = useParams();
  const { lang } = useI18n();
  const navigate = useNavigate();

  const [status, setStatus] = useState("loading");
  const [error, setError] = useState(null);

  useEffect(() => {
    document.title = "Verify your report — Luqya";
  }, []);

  useEffect(() => {
    let cancelled = false;

    confirmReporterClaim(token)
      .then(() => {
        if (cancelled) return;
        setStatus("success");
      })
      .catch((err) => {
        if (cancelled) return;
        setStatus("error");
        setError(
          err.message ||
            copy(lang, {
              ar: "تعذّر التحقق من هذا الرابط. حاول مرة أخرى.",
              en: "Couldn't verify this link. Please try again.",
              ur: "اس لنک کی تصدیق نہیں ہو سکی۔ دوبارہ کوشش کریں۔",
            })
        );
      });

    return () => {
      cancelled = true;
    };
  }, [token, lang]);

  if (status === "loading") {
    return (
      <div className="flex items-center justify-center gap-2 py-32 text-muted-foreground">
        <Loader2 className="size-5 animate-spin" />
        {copy(lang, { ar: "جارٍ التحقق من الرابط...", en: "Verifying your link...", ur: "لنک کی تصدیق ہو رہی ہے..." })}
      </div>
    );
  }

  if (status === "error") {
    return (
      <div className="flex flex-col items-center justify-center gap-3 py-32 px-6 text-center">
        <AlertCircle className="size-7 text-error" />
        <p className="max-w-md text-sm text-muted-foreground">{error}</p>
        <button
          type="button"
          onClick={() => navigate("/")}
          className="mt-2 text-sm font-semibold text-primary hover:underline"
        >
          {copy(lang, { ar: "العودة إلى الرئيسية", en: "Back to home", ur: "ہوم پر واپس جائیں" })}
        </button>
      </div>
    );
  }

  return (
    <div className="flex flex-col items-center justify-center gap-3 py-32 px-6 text-center">
      <CheckCircle2 className="size-7 text-success" />
      <p className="max-w-md text-sm text-foreground">
        {copy(lang, {
          ar: "تم التحقق من بلاغك بنجاح. يمكنك الآن التواصل داخل المنصة مع من وجده.",
          en: "Your report is verified. You can now message the person who found it right here on Luqya.",
          ur: "آپ کی رپورٹ کی تصدیق ہو گئی۔ اب آپ اسے تلاش کرنے والے سے یہاں پیغام کر سکتے ہیں۔",
        })}
      </p>
      <button
        type="button"
        onClick={() => navigate("/messages")}
        className="mt-2 text-sm font-semibold text-primary hover:underline"
      >
        {copy(lang, { ar: "الذهاب إلى الرسائل", en: "Go to messages", ur: "پیغامات پر جائیں" })}
      </button>
    </div>
  );
}
