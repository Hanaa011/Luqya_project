import { useState } from "react";
import { Link, useNavigate } from "react-router-dom";
import { Loader2, ArrowRight, AlertCircle } from "lucide-react";
import AuthShell from "../../components/AuthShell";
import { useI18n } from "../../lib/useI18n";
import { useAuth } from "../../lib/useAuth";
import { updateMyProfile } from "../../api/auth";
import { ApiError } from "../../api/httpClient";
import { isValidSaudiMobile, normalizeSaudiMobile } from "../../lib/saudiPhone";

export default function Register() {
  const { t, dir, locale } = useI18n();
  const navigate = useNavigate();
  const { register, login, refreshProfile } = useAuth();

  const [loading, setLoading] = useState(false);
  const [error, setError] = useState(null);
  const [phoneError, setPhoneError] = useState(false);
  const [form, setForm] = useState({
    firstName: "",
    lastName: "",
    email: "",
    phone: "",
    password: "",
  });

  function update(field, value) {
    setForm((f) => ({ ...f, [field]: value }));
  }

  async function handleSubmit(event) {
    event.preventDefault();
    setError(null);

    if (!isValidSaudiMobile(form.phone)) {
      setPhoneError(true);
      return;
    }
    setPhoneError(false);
    setLoading(true);

    try {
      // RegisterDto only has userName/emailAddress/password/appName — the
      // form's real name and phone go on the profile in a follow-up call.
      await register({
        userName: form.email,
        emailAddress: form.email,
        password: form.password,
      });

      // RegisterAsync doesn't sign the user in — log in explicitly right
      // after with the same credentials via the real /connect/token flow.
      await login({ userNameOrEmailAddress: form.email, password: form.password });

      try {
        // Not a silent catch anymore — if this fails, the account exists
        // but has no phone on file, which is a real problem worth
        // surfacing distinctly (see requirement 1C), not hiding.
        //
        // userName/email are required here even though they're unchanged:
        // ABP's built-in ProfileAppService.UpdateAsync always calls
        // UserManager.SetUserNameAsync(user, input.UserName) first — if
        // UserName arrives blank, ASP.NET Identity rejects it and ABP's
        // BusinessException wrapper surfaces that as an HTTP 403 (not 400),
        // which is what was breaking this call. The user was registered
        // with userName === emailAddress === form.email, so echo that back.
        await updateMyProfile({
          userName: form.email,
          email: form.email,
          name: form.firstName,
          surname: form.lastName,
          phoneNumber: normalizeSaudiMobile(form.phone),
        });
      } catch (profileErr) {
        const phoneFallback = {
          ar: "تم إنشاء الحساب، لكن تعذّر حفظ رقم الهاتف. يمكنك إضافته لاحقًا.",
          en: "Your account was created, but we couldn't save your phone number. You can add it later.",
          ur: "آپ کا اکاؤنٹ بن گیا، لیکن فون نمبر محفوظ نہیں ہو سکا۔ آپ بعد میں شامل کر سکتے ہیں۔",
        }[locale];
        setError(profileErr instanceof ApiError ? profileErr.message || phoneFallback : phoneFallback);
        await refreshProfile();
        setLoading(false);
        return;
      }

      // login() already refreshed the in-memory profile before the phone
      // was saved, so it's stale at this point — refresh again so
      // profile.phoneNumber is accurate immediately (e.g. if the very
      // next thing the user does is create a report).
      await refreshProfile();

      navigate("/dashboard?welcome=1");
    } catch (err) {
      const fallback = {
        ar: "تعذّر إنشاء الحساب. حاول مرة أخرى.",
        en: "Couldn't create your account. Please try again.",
        ur: "اکاؤنٹ نہیں بن سکا۔ دوبارہ کوشش کریں۔",
      }[locale];

      if (err instanceof ApiError && err.isValidation) {
        setError(err.validationErrors.map((v) => v.message).join(" ") || err.message || fallback);
      } else {
        setError(err.message || fallback);
      }
    } finally {
      setLoading(false);
    }
  }

  return (
    <AuthShell eyebrow={t("registerEyebrow")} title={t("registerTitle")} subtitle={t("registerSub")}>
      {error && (
        <div className="mb-5 flex items-start gap-2.5 rounded-2xl bg-error-tint text-error px-4 py-3 text-sm">
          <AlertCircle className="size-4 shrink-0 mt-0.5" />
          <span>{error}</span>
        </div>
      )}

      <form onSubmit={handleSubmit} className="space-y-4">
        <div className="grid grid-cols-2 gap-3">
          <input
            type="text"
            name="given-name"
            autoComplete="given-name"
            required
            value={form.firstName}
            onChange={(e) => update("firstName", e.target.value)}
            placeholder={t("firstNamePh")}
            className="w-full px-5 py-3.5 rounded-2xl bg-stone-50 border border-stone-200 focus:border-primary focus:outline-none focus:ring-2 focus:ring-primary/10 transition-all"
          />
          <input
            type="text"
            name="family-name"
            autoComplete="family-name"
            required
            value={form.lastName}
            onChange={(e) => update("lastName", e.target.value)}
            placeholder={t("lastNamePh")}
            className="w-full px-5 py-3.5 rounded-2xl bg-stone-50 border border-stone-200 focus:border-primary focus:outline-none focus:ring-2 focus:ring-primary/10 transition-all"
          />
        </div>

        <input
          type="email"
          name="email"
          autoComplete="email"
          required
          value={form.email}
          onChange={(e) => update("email", e.target.value)}
          placeholder={t("emailPh")}
          className="w-full px-5 py-3.5 rounded-2xl bg-stone-50 border border-stone-200 focus:border-primary focus:outline-none focus:ring-2 focus:ring-primary/10 transition-all"
        />

        <div>
          <input
            type="tel"
            name="tel"
            autoComplete="tel"
            required
            dir="ltr"
            value={form.phone}
            onChange={(e) => {
              update("phone", e.target.value);
              setPhoneError(false);
            }}
            placeholder="+966 5x xxx xxxx"
            className="w-full px-5 py-3.5 rounded-2xl bg-stone-50 border border-stone-200 focus:border-primary focus:outline-none focus:ring-2 focus:ring-primary/10 transition-all text-start"
          />
          {phoneError && (
            <p className="text-xs text-error mt-1.5">{t("fldMobileInvalid")}</p>
          )}
        </div>

        <input
          type="password"
          name="new-password"
          autoComplete="new-password"
          required
          value={form.password}
          onChange={(e) => update("password", e.target.value)}
          placeholder={t("passwordPh")}
          className="w-full px-5 py-3.5 rounded-2xl bg-stone-50 border border-stone-200 focus:border-primary focus:outline-none focus:ring-2 focus:ring-primary/10 transition-all"
        />

        <label className="flex items-start gap-2.5 text-xs text-muted-foreground pt-1">
          <input
            type="checkbox"
            required
            className="mt-0.5 size-4 rounded border-stone-300 text-primary focus:ring-primary/20"
          />
          {t("agreeTerms")}
        </label>

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
          {t("createAccountCta")}
        </button>
      </form>

      <p className="mt-8 text-center text-sm text-muted-foreground">
        {t("haveAccount")}{" "}
        <Link to="/auth/login" className="font-semibold text-primary hover:underline">
          {t("logInLink")}
        </Link>
      </p>
    </AuthShell>
  );
}
