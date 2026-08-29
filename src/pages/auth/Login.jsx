import { useState } from "react";
import { Link, useLocation, useNavigate } from "react-router-dom";
import { Eye, EyeOff, Loader2, ArrowRight, AlertCircle, ArrowUpRight } from "lucide-react";
import AuthShell from "../../components/AuthShell";
import { useI18n } from "../../lib/useI18n";
import { useAuth } from "../../lib/useAuth";

function copy(locale, values) {
  return values[locale] ?? values.en;
}

export default function Login() {
  const { t, dir, locale } = useI18n();
  const navigate = useNavigate();
  const location = useLocation();
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

      // Preserve the route the user originally tried to access.
      const from = location.state?.from;
      navigate(from || "/dashboard", {
        state: location.state?.fromState,
      });
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
    <AuthShell
      eyebrow={t("loginEyebrow")}
      title={t("loginTitle")}
      subtitle={t("loginSub")}
    >
      {/* Minimal top-corner link on the light/auth side. */}
      <Link
        to="/"
        aria-label={copy(locale, {
          ar: "العودة إلى الرئيسية",
          en: "Back to home",
          ur: "ہوم پر واپس جائیں",
        })}
        className="
          fixed right-6 top-5 z-20
          inline-flex items-center gap-1.5
          text-[11px] font-semibold tracking-[0.01em]
          text-muted-foreground/60
          transition-colors duration-200
          hover:text-primary
          focus-visible:outline-none
          focus-visible:text-primary
          focus-visible:underline
          sm:right-8 sm:top-7
          lg:right-10 lg:top-8
        "
      >
        <span>
          {copy(locale, {
            ar: "الرئيسية",
            en: "Home",
            ur: "ہوم",
          })}
        </span>

        <ArrowUpRight
          className="size-3"
          strokeWidth={1.6}
          aria-hidden="true"
        />
      </Link>

      {error && (
        <div className="mb-5 flex items-start gap-2.5 rounded-2xl bg-error-tint px-4 py-3 text-sm text-error">
          <AlertCircle className="mt-0.5 size-4 shrink-0" />
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
          className="w-full rounded-2xl border border-stone-200 bg-stone-50 px-5 py-3.5 transition-all focus:border-primary focus:outline-none focus:ring-2 focus:ring-primary/10"
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
            className="w-full rounded-2xl border border-stone-200 bg-stone-50 px-5 py-3.5 pe-12 transition-all focus:border-primary focus:outline-none focus:ring-2 focus:ring-primary/10"
          />

          <button
            type="button"
            onClick={() => setShowPassword((v) => !v)}
            aria-label={copy(locale, {
              ar: showPassword ? "إخفاء كلمة المرور" : "إظهار كلمة المرور",
              en: showPassword ? "Hide password" : "Show password",
              ur: showPassword ? "پاس ورڈ چھپائیں" : "پاس ورڈ دکھائیں",
            })}
            className="absolute inset-y-0 end-3 grid w-9 place-items-center rounded-lg text-muted-foreground transition-colors hover:text-foreground focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary/20"
          >
            {showPassword ? (
              <EyeOff className="size-4" />
            ) : (
              <Eye className="size-4" />
            )}
          </button>
        </div>

        <div className="-mt-1 flex justify-end">
          <Link
            to="/auth/forgot-password"
            className="text-xs font-semibold text-primary hover:underline"
          >
            {t("forgotLink")}
          </Link>
        </div>

        <button
          type="submit"
          disabled={loading}
          className="inline-flex w-full items-center justify-center gap-2 rounded-2xl bg-primary px-6 py-3.5 font-semibold text-primary-foreground shadow-glow transition-transform hover:-translate-y-0.5 disabled:translate-y-0 disabled:opacity-70"
        >
          {loading ? (
            <Loader2 className="size-4 animate-spin" />
          ) : (
            <ArrowRight
              className={`size-4 ${dir === "rtl" ? "rotate-180" : ""}`}
            />
          )}
          {t("loginSubmit")}
        </button>

      </form>

      <p className="mt-8 text-center text-sm text-muted-foreground">
        {t("noAccount")}{" "}
        <Link
          to="/auth/register"
          state={location.state}
          className="font-semibold text-primary hover:underline"
        >
          {t("createOne")}
        </Link>
      </p>

    </AuthShell>
  );
}
