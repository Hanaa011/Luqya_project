import { useState } from "react";
import { Link, useNavigate } from "react-router-dom";
import { Loader2, ArrowRight, ArrowLeft, KeyRound, AlertCircle } from "lucide-react";
import AuthShell from "../../components/AuthShell";
import { useI18n } from "../../lib/useI18n";
import { sendPasswordResetCode } from "../../api/auth";

export default function ForgotPassword() {
  const { t, dir, locale } = useI18n();
  const navigate = useNavigate();
  const [email, setEmail] = useState("");
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState(null);

  async function handleSubmit(event) {
    event.preventDefault();
    setError(null);
    setLoading(true);

    try {
      await sendPasswordResetCode({ email });
      navigate(`/auth/verify?flow=reset&email=${encodeURIComponent(email)}`);
    } catch {
      setError(
        {
          ar: "تعذّر إرسال رمز التحقق. تأكد من البريد الإلكتروني وحاول مرة أخرى.",
          en: "Couldn't send the reset code. Check the email and try again.",
          ur: "کوڈ نہیں بھیجا جا سکا۔ ای میل چیک کر کے دوبارہ کوشش کریں۔",
        }[locale]
      );
    } finally {
      setLoading(false);
    }
  }

  return (
    <AuthShell
      eyebrow={t("forgotEyebrow")}
      title={t("forgotTitle")}
      subtitle={t("forgotSub")}
    >
      <div className="size-12 rounded-2xl bg-primary/10 text-primary grid place-items-center mb-6">
        <KeyRound className="size-5" />
      </div>

      {error && (
        <div className="mb-5 flex items-start gap-2.5 rounded-2xl bg-error-tint text-error px-4 py-3 text-sm">
          <AlertCircle className="size-4 shrink-0 mt-0.5" />
          <span>{error}</span>
        </div>
      )}

      <form onSubmit={handleSubmit} className="space-y-4">
        <input
          type="email"
          required
          value={email}
          onChange={(e) => setEmail(e.target.value)}
          placeholder={t("emailPh")}
          className="w-full px-5 py-3.5 rounded-2xl bg-stone-50 border border-stone-200 focus:border-primary focus:outline-none focus:ring-2 focus:ring-primary/10 transition-all"
        />

        <button
          type="submit"
          disabled={loading}
          className="w-full inline-flex items-center justify-center gap-2 bg-primary text-primary-foreground px-6 py-3.5 rounded-2xl font-semibold shadow-glow hover:-translate-y-0.5 transition-transform disabled:opacity-70 disabled:translate-y-0"
        >
          {loading ? (
            <Loader2 className="size-4 animate-spin" />
          ) : (
            <ArrowRight className={`size-4 ${dir === "rtl" ? "rotate-180" : ""}`} />
          )}
          {t("sendResetCta")}
        </button>
      </form>

      <Link
        to="/auth/login"
        className="mt-8 inline-flex items-center gap-2 text-sm font-semibold text-muted-foreground hover:text-primary transition-colors"
      >
        <ArrowLeft className={`size-4 ${dir === "rtl" ? "rotate-180" : ""}`} />
        {t("backToLogin")}
      </Link>
    </AuthShell>
  );
}
