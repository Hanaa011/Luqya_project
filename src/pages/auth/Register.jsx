import { useMemo, useRef, useState } from "react";
import { Link, useLocation, useNavigate } from "react-router-dom";
import {
  AlertCircle,
  ArrowRight,
  ArrowUpRight,
  Check,
  Eye,
  EyeOff,
  Loader2,
} from "lucide-react";

import AuthShell from "../../components/AuthShell";
import { useI18n } from "../../lib/useI18n";
import { useAuth } from "../../lib/useAuth";
import { updateMyProfile } from "../../api/auth";
import { ApiError } from "../../api/httpClient";
import {
  isValidSaudiMobile,
  normalizeSaudiMobile,
  normalizeSaudiPhoneInput,
} from "../../lib/saudiPhone";

const PASSWORD_RULES = [
  { key: "length", test: (value) => value.length >= 8 },
  { key: "uppercase", test: (value) => /[A-Z]/.test(value) },
  { key: "lowercase", test: (value) => /[a-z]/.test(value) },
  { key: "number", test: (value) => /[0-9]/.test(value) },
  { key: "special", test: (value) => /[^A-Za-z0-9\s]/.test(value) },
];

function uiCopy(locale, values) {
  return values[locale] ?? values.en;
}

const COPY = {
  ar: {
    confirmPassword: "تأكيد كلمة المرور",
    passwordMismatch: "كلمتا المرور غير متطابقتين.",
    passwordInvalid: "كلمة المرور لا تستوفي جميع المتطلبات.",
    strength: "قوة كلمة المرور",
    weak: "ضعيفة",
    medium: "متوسطة",
    strong: "قوية",
    requirements: {
      length: "8 أحرف على الأقل",
      uppercase: "حرف إنجليزي كبير (A-Z)",
      lowercase: "حرف إنجليزي صغير (a-z)",
      number: "رقم واحد على الأقل (0-9)",
      special: "رمز خاص واحد على الأقل (! @ # $ ...)",
    },
    show: "إظهار كلمة المرور",
    hide: "إخفاء كلمة المرور",
  },
  en: {
    confirmPassword: "Confirm password",
    passwordMismatch: "Passwords do not match.",
    passwordInvalid: "Password does not meet all requirements.",
    strength: "Password strength",
    weak: "Weak",
    medium: "Medium",
    strong: "Strong",
    requirements: {
      length: "At least 8 characters",
      uppercase: "One uppercase letter (A-Z)",
      lowercase: "One lowercase letter (a-z)",
      number: "At least one number (0-9)",
      special: "At least one special character (! @ # $ ...)",
    },
    show: "Show password",
    hide: "Hide password",
  },
  ur: {
    confirmPassword: "پاس ورڈ کی تصدیق",
    passwordMismatch: "دونوں پاس ورڈ ایک جیسے نہیں ہیں۔",
    passwordInvalid: "پاس ورڈ تمام شرائط پوری نہیں کرتا۔",
    strength: "پاس ورڈ کی مضبوطی",
    weak: "کمزور",
    medium: "درمیانہ",
    strong: "مضبوط",
    requirements: {
      length: "کم از کم 8 حروف",
      uppercase: "ایک بڑا انگریزی حرف (A-Z)",
      lowercase: "ایک چھوٹا انگریزی حرف (a-z)",
      number: "کم از کم ایک عدد (0-9)",
      special: "کم از کم ایک خاص علامت (! @ # $ ...)",
    },
    show: "پاس ورڈ دکھائیں",
    hide: "پاس ورڈ چھپائیں",
  },
};

function validatePassword(password) {
  const checks = Object.fromEntries(
    PASSWORD_RULES.map((rule) => [rule.key, rule.test(password)])
  );

  const passed = Object.values(checks).filter(Boolean).length;

  return {
    checks,
    passed,
    valid: passed === PASSWORD_RULES.length,
  };
}

function getStrength(passed) {
  if (passed <= 2) return "weak";
  if (passed <= 4) return "medium";
  return "strong";
}

export default function Register() {
  const { t, dir, locale } = useI18n();
  const navigate = useNavigate();
  const location = useLocation();
  const { register, login, refreshProfile } = useAuth();

  const passwordRef = useRef(null);
  const confirmPasswordRef = useRef(null);

  const [loading, setLoading] = useState(false);
  const [error, setError] = useState(null);
  const [phoneError, setPhoneError] = useState(false);
  const [passwordError, setPasswordError] = useState(false);
  const [confirmError, setConfirmError] = useState(false);
  const [showPassword, setShowPassword] = useState(false);
  const [showConfirmPassword, setShowConfirmPassword] = useState(false);
  const [passwordFocused, setPasswordFocused] = useState(false);

  const [form, setForm] = useState({
    firstName: "",
    lastName: "",
    email: "",
    phone: "",
    password: "",
    confirmPassword: "",
  });

  const copy = COPY[locale] ?? COPY.en;

  const passwordStatus = useMemo(
    () => validatePassword(form.password),
    [form.password]
  );

  const strength = getStrength(passwordStatus.passed);

  const passwordsMatch =
    form.confirmPassword.length > 0 &&
    form.password === form.confirmPassword;

  const showPasswordHelp =
    passwordFocused || Boolean(form.password) || passwordError;

  function update(field, value) {
    setForm((current) => ({ ...current, [field]: value }));
  }

  async function handleSubmit(event) {
    event.preventDefault();
    setError(null);

    if (!isValidSaudiMobile(form.phone)) {
      setPhoneError(true);
      return;
    }

    setPhoneError(false);

    if (!passwordStatus.valid) {
      setPasswordError(true);
      passwordRef.current?.focus();
      return;
    }

    setPasswordError(false);

    if (form.password !== form.confirmPassword) {
      setConfirmError(true);
      setError(copy.passwordMismatch);
      confirmPasswordRef.current?.focus();
      return;
    }

    setConfirmError(false);
    setLoading(true);

    try {
      await register({
        userName: form.email,
        emailAddress: form.email,
        password: form.password,
      });

      await login({
        userNameOrEmailAddress: form.email,
        password: form.password,
      });

      try {
        await updateMyProfile({
          userName: form.email,
          email: form.email,
          name: form.firstName,
          surname: form.lastName,
          phoneNumber: normalizeSaudiMobile(form.phone),
        });
      } catch (profileErr) {
        if (
          profileErr instanceof ApiError &&
          profileErr.code === "LostFound:Account:PhoneAlreadyRegistered"
        ) {
          setError(
            {
              ar: "رقم الجوال هذا مسجل مسبقًا بحساب آخر.",
              en: "This phone number is already registered to another account.",
              ur: "یہ فون نمبر پہلے سے کسی دوسرے اکاؤنٹ سے رجسٹرڈ ہے۔",
            }[locale]
          );

          await refreshProfile();
          setLoading(false);
          return;
        }

        const phoneFallback = {
          ar: "تم إنشاء الحساب، لكن تعذّر حفظ رقم الهاتف. يمكنك إضافته لاحقًا.",
          en: "Your account was created, but we couldn't save your phone number. You can add it later.",
          ur: "آپ کا اکاؤنٹ بن گیا، لیکن فون نمبر محفوظ نہیں ہو سکا۔ آپ بعد میں شامل کر سکتے ہیں۔",
        }[locale];

        setError(
          profileErr instanceof ApiError
            ? profileErr.message || phoneFallback
            : phoneFallback
        );

        await refreshProfile();
        setLoading(false);
        return;
      }

      await refreshProfile();

      // Mirrors Login.jsx's own restore: a registration triggered by
      // RequireAuth (e.g. the /claim/:token flow) carries the page the
      // visitor was trying to reach - land them back there instead of
      // always going to the dashboard, so the claim token isn't lost by
      // choosing "create account" instead of "log in".
      const from = location.state?.from;
      if (from) {
        navigate(from, { state: location.state?.fromState });
      } else {
        navigate("/dashboard?welcome=1");
      }
    } catch (err) {
      const fallback = {
        ar: "تعذّر إنشاء الحساب. حاول مرة أخرى.",
        en: "Couldn't create your account. Please try again.",
        ur: "اکاؤنٹ نہیں بن سکا۔ دوبارہ کوشش کریں۔",
      }[locale];

      if (err instanceof ApiError && err.isValidation) {
        setError(
          err.validationErrors.map((v) => v.message).join(" ") ||
            err.message ||
            fallback
        );
      } else {
        setError(err.message || fallback);
      }
    } finally {
      setLoading(false);
    }
  }

  return (
    <AuthShell
      eyebrow={t("registerEyebrow")}
      title={t("registerTitle")}
      subtitle={t("registerSub")}
    >
      {/* Minimal return link, aligned with the auth page chrome rather than the form. */}
      <Link
        to="/"
        aria-label={uiCopy(locale, {
          ar: "العودة إلى الرئيسية",
          en: "Back to home",
          ur: "ہوم پر واپس جائیں",
        })}
        className="
          absolute right-6 top-5 z-20
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
          {uiCopy(locale, {
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
        <div
          role="alert"
          className="mb-5 flex items-start gap-2.5 rounded-2xl border border-error/20 bg-error/5 px-4 py-3 text-sm text-error"
        >
          <AlertCircle className="mt-0.5 size-4 shrink-0" />
          <span>{error}</span>
        </div>
      )}

      <form onSubmit={handleSubmit} className="space-y-4">
        <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
          <input
            type="text"
            name="given-name"
            autoComplete="given-name"
            required
            value={form.firstName}
            onChange={(e) => update("firstName", e.target.value)}
            placeholder={t("firstNamePh")}
            className="w-full rounded-2xl border border-stone-200 bg-stone-50 px-5 py-3.5 transition-all focus:border-primary focus:outline-none focus:ring-2 focus:ring-primary/10"
          />

          <input
            type="text"
            name="family-name"
            autoComplete="family-name"
            required
            value={form.lastName}
            onChange={(e) => update("lastName", e.target.value)}
            placeholder={t("lastNamePh")}
            className="w-full rounded-2xl border border-stone-200 bg-stone-50 px-5 py-3.5 transition-all focus:border-primary focus:outline-none focus:ring-2 focus:ring-primary/10"
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
          className="w-full rounded-2xl border border-stone-200 bg-stone-50 px-5 py-3.5 transition-all focus:border-primary focus:outline-none focus:ring-2 focus:ring-primary/10"
        />

        <div>
          <input
            type="tel"
            inputMode="tel"
            name="tel"
            autoComplete="tel"
            required
            dir="ltr"
            value={form.phone}
            onChange={(e) => {
              update("phone", normalizeSaudiPhoneInput(e.target.value));
              setPhoneError(false);
            }}
            placeholder="05XXXXXXXX"
            className="w-full rounded-2xl border border-stone-200 bg-stone-50 px-5 py-3.5 text-start transition-all focus:border-primary focus:outline-none focus:ring-2 focus:ring-primary/10"
          />

          {phoneError && (
            <p className="mt-1.5 text-xs text-error">
              {t("fldMobileInvalid")}
            </p>
          )}
        </div>

        {/* Password */}
        <div className="space-y-2">
          <div className="relative">
            <input
              ref={passwordRef}
              type={showPassword ? "text" : "password"}
              name="new-password"
              autoComplete="new-password"
              required
              value={form.password}
              onFocus={() => setPasswordFocused(true)}
              onBlur={() => setPasswordFocused(false)}
              onChange={(e) => {
                update("password", e.target.value);
                setPasswordError(false);
                setConfirmError(false);

                if (error === copy.passwordMismatch) {
                  setError(null);
                }
              }}
              placeholder={t("passwordPh")}
              className={`w-full rounded-2xl border bg-stone-50 py-3.5 ps-5 pe-14 transition-all focus:outline-none focus:ring-2 focus:ring-primary/10 [&::-ms-clear]:hidden [&::-ms-reveal]:hidden ${
                passwordError
                  ? "border-error focus:border-error"
                  : "border-stone-200 focus:border-primary"
              }`}
            />

            <button
              type="button"
              onClick={() => setShowPassword((value) => !value)}
              aria-label={showPassword ? copy.hide : copy.show}
              className={`absolute inset-y-0 grid w-10 place-items-center text-muted-foreground transition-colors hover:text-foreground focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary/20 ${
                dir === "rtl" ? "left-2" : "right-2"
              }`}
            >
              {showPassword ? (
                <EyeOff className="size-4" />
              ) : (
                <Eye className="size-4" />
              )}
            </button>
          </div>

          <div
            className={`grid transition-[grid-template-rows,opacity] duration-300 ease-out ${
              showPasswordHelp
                ? "grid-rows-[1fr] opacity-100"
                : "grid-rows-[0fr] opacity-0"
            }`}
            aria-hidden={!showPasswordHelp}
          >
            <div className="min-h-0 overflow-hidden">
              <div
                className={`rounded-xl border border-border/70 bg-stone-50/70 px-4 py-3 transition-transform duration-300 ease-out ${
                  showPasswordHelp ? "translate-y-0" : "-translate-y-1"
                }`}
              >
                <div className="mb-2 flex items-center justify-between text-xs">
                  <span className="text-muted-foreground">{copy.strength}</span>
                  <span
                    className={`font-semibold transition-colors duration-200 ${
                      strength === "strong"
                        ? "text-success"
                        : strength === "medium"
                          ? "text-amber-600"
                          : "text-muted-foreground"
                    }`}
                  >
                    {copy[strength]}
                  </span>
                </div>

                <div className="mb-3 flex gap-1">
                  {[1, 2, 3].map((step) => (
                    <span
                      key={step}
                      className={`h-1 flex-1 rounded-full transition-colors duration-300 ${
                        (strength === "weak" && step === 1) ||
                        (strength === "medium" && step <= 2) ||
                        strength === "strong"
                          ? strength === "strong"
                            ? "bg-success"
                            : strength === "medium"
                              ? "bg-amber-500"
                              : "bg-muted-foreground/50"
                          : "bg-stone-200"
                      }`}
                    />
                  ))}
                </div>

                <ul className="space-y-1.5">
                  {PASSWORD_RULES.map((rule) => {
                    const passed = passwordStatus.checks[rule.key];

                    return (
                      <li
                        key={rule.key}
                        className={`flex items-center gap-2 text-xs transition-colors duration-200 ${
                          passed
                            ? "text-success"
                            : passwordError
                              ? "text-error"
                              : "text-muted-foreground"
                        }`}
                      >
                        <Check
                          className={`size-3.5 transition-all duration-200 ${
                            passed ? "scale-100 opacity-100" : "scale-90 opacity-25"
                          }`}
                        />
                        <span>{copy.requirements[rule.key]}</span>
                      </li>
                    );
                  })}
                </ul>

                {passwordError && (
                  <p role="alert" className="mt-2 text-xs font-medium text-error">
                    {copy.passwordInvalid}
                  </p>
                )}
              </div>
            </div>
          </div>
        </div>

        {/* Confirm password */}
        <div>
          <div className="relative">
            <input
              ref={confirmPasswordRef}
              type={showConfirmPassword ? "text" : "password"}
              name="confirm-password"
              autoComplete="new-password"
              required
              value={form.confirmPassword}
              onChange={(e) => {
                update("confirmPassword", e.target.value);
                setConfirmError(false);

                if (error === copy.passwordMismatch) {
                  setError(null);
                }
              }}
              placeholder={copy.confirmPassword}
              className={`w-full rounded-2xl border bg-stone-50 py-3.5 ps-5 pe-14 transition-all focus:outline-none focus:ring-2 focus:ring-primary/10 [&::-ms-clear]:hidden [&::-ms-reveal]:hidden ${
                confirmError
                  ? "border-error focus:border-error"
                  : passwordsMatch
                    ? "border-success/50 focus:border-success"
                    : "border-stone-200 focus:border-primary"
              }`}
            />

            <button
              type="button"
              onClick={() => setShowConfirmPassword((value) => !value)}
              aria-label={showConfirmPassword ? copy.hide : copy.show}
              className={`absolute inset-y-0 grid w-10 place-items-center text-muted-foreground transition-colors hover:text-foreground focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary/20 ${
                dir === "rtl" ? "left-2" : "right-2"
              }`}
            >
              {showConfirmPassword ? (
                <EyeOff className="size-4" />
              ) : (
                <Eye className="size-4" />
              )}
            </button>
          </div>

        </div>

        <label className="flex items-start gap-2.5 pt-1 text-xs text-muted-foreground">
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
          className="inline-flex w-full items-center justify-center gap-2 rounded-2xl bg-primary px-6 py-3.5 font-semibold text-primary-foreground shadow-glow transition-transform hover:-translate-y-0.5 disabled:translate-y-0 disabled:opacity-70"
        >
          {loading ? (
            <Loader2 className="size-4 animate-spin" />
          ) : (
            <ArrowRight
              className={`size-4 ${dir === "rtl" ? "rotate-180" : ""}`}
            />
          )}
          {t("createAccountCta")}
        </button>
      </form>

      <p className="mt-8 text-center text-sm text-muted-foreground">
        {t("haveAccount")}{" "}
        <Link
          to="/auth/login"
          className="font-semibold text-primary hover:underline"
        >
          {t("logInLink")}
        </Link>
      </p>
    </AuthShell>
  );
}
