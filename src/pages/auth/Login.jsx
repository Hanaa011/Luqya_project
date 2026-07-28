import { useState } from "react";
import { Link, useNavigate } from "react-router-dom";
import { Eye, EyeOff, Loader2, ArrowRight, AlertCircle } from "lucide-react";
import AuthShell from "../../components/AuthShell";
import { useI18n } from "../../lib/useI18n";
import { useAuth } from "../../lib/useAuth";

export default function Login() {
  const { t, dir, locale } = useI18n();
  const navigate = useNavigate();
  const { login } = useAuth();

  const [showPassword, setShowPassword] = useState(false);
  const [loading, setLoading] = useState(false);
  const [userNameOrEmail, setUserNameOrEmail] = useState("");
  const [password, setPassword] = useState("");
  const [error, setError] = useState(null);

  async function handleSubmit(event) {
    event.preventDefault();
    setError(null);
    setLoading(true);

    try {
      await login({ userNameOrEmailAddress: userNameOrEmail, password });
      navigate("/dashboard");
    } catch (err) {
      const fallback = {
        ar: "تعذّر تسجيل الدخول. تحقق من بياناتك وحاول مرة أخرى.",
        en: "Couldn't log in. Check your details and try again.",
        ur: "لاگ ان نہیں ہو سکا۔ تفصیلات چیک کر کے دوبارہ کوشش کریں۔",
      }[locale];

      setError(
        err.reason === "invalidCredentials"
          ? {
              ar: "اسم المستخدم أو البريد الإلكتروني أو كلمة المرور غير صحيحة.",
              en: "Incorrect username, email, or password.",
              ur: "یوزر نیم، ای میل یا پاس ورڈ غلط ہے۔",
            }[locale]
          : fallback
      );
    } finally {
      setLoading(false);
    }
  }

  return (
    <AuthShell eyebrow={t("loginEyebrow")} title={t("loginTitle")} subtitle={t("loginSub")}>
      {error && (
        <div className="mb-5 flex items-start gap-2.5 rounded-2xl bg-error-tint text-error px-4 py-3 text-sm">
          <AlertCircle className="size-4 shrink-0 mt-0.5" />
          <span>{error}</span>
        </div>
      )}

      <form onSubmit={handleSubmit} className="space-y-4">
        <input
          type="text"
          name="username"
          required
          autoComplete="username"
          value={userNameOrEmail}
          onChange={(e) => setUserNameOrEmail(e.target.value)}
          placeholder={t("emailPh")}
          className="w-full px-5 py-3.5 rounded-2xl bg-stone-50 border border-stone-200 focus:border-primary focus:outline-none focus:ring-2 focus:ring-primary/10 transition-all"
        />

        <div className="relative">
          <input
            type={showPassword ? "text" : "password"}
            name="current-password"
            required
            autoComplete="current-password"
            value={password}
            onChange={(e) => setPassword(e.target.value)}
            placeholder={t("passwordPh")}
            className="w-full px-5 py-3.5 rounded-2xl bg-stone-50 border border-stone-200 focus:border-primary focus:outline-none focus:ring-2 focus:ring-primary/10 transition-all"
          />

          <button
            type="button"
            onClick={() => setShowPassword((v) => !v)}
            className="absolute inset-y-0 end-4 grid place-items-center text-muted-foreground hover:text-foreground"
          >
            {showPassword ? <EyeOff className="size-4" /> : <Eye className="size-4" />}
          </button>
        </div>

        <div className="flex justify-end -mt-1">
          <Link to="/auth/forgot-password" className="text-xs font-semibold text-primary hover:underline">
            {t("forgotLink")}
          </Link>
        </div>

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
          {t("loginSubmit")}
        </button>
      </form>

      <p className="mt-8 text-center text-sm text-muted-foreground">
        {t("noAccount")}{" "}
        <Link to="/auth/register" className="font-semibold text-primary hover:underline">
          {t("createOne")}
        </Link>
      </p>
    </AuthShell>
  );
}
