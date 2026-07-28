import { useRef, useState } from "react";
import { useNavigate, useSearchParams } from "react-router-dom";
import { Check, AlertCircle, Loader2, ArrowRight } from "lucide-react";
import AuthShell from "../../components/AuthShell";
import DammaMark from "../../components/DammaMark";
import { useI18n } from "../../lib/useI18n";
import { getUserByEmailBestEffort, verifyPasswordResetToken, resetPassword } from "../../api/auth";

const LENGTH = 6;

export default function VerifyOtp() {
  const { t, locale, dir } = useI18n();
  const navigate = useNavigate();
  const [params] = useSearchParams();
  const flow = params.get("flow") ?? "reset";
  const email = params.get("email") ?? "";

  const [digits, setDigits] = useState(Array(LENGTH).fill(""));
  const [status, setStatus] = useState("idle"); // idle | verifying | password | saving | success | error
  const [errorMsg, setErrorMsg] = useState(null);
  const [userId, setUserId] = useState(null);
  const [newPassword, setNewPassword] = useState("");
  const inputsRef = useRef([]);

  async function runVerification(code) {
    setStatus("verifying");
    setErrorMsg(null);

    try {
      const user = await getUserByEmailBestEffort(email);
      const resolvedUserId = user.id;

      const valid = await verifyPasswordResetToken({ userId: resolvedUserId, resetToken: code });
      if (!valid) throw new Error("invalid");

      setUserId(resolvedUserId);
      setStatus("password");
    } catch {
      setStatus("error");
      setErrorMsg(
        {
          ar: "رمز غير صحيح أو منتهي الصلاحية. تحقق منه أو اطلب رمزًا جديدًا.",
          en: "That code is invalid or expired. Check it or request a new one.",
          ur: "کوڈ غلط یا میعاد ختم ہو چکا ہے۔ چیک کریں یا نیا کوڈ لیں۔",
        }[locale]
      );
    }
  }

  function updateDigit(index, value) {
    if (status !== "idle" && status !== "error") return;
    const clean = value.replace(/[^0-9]/g, "").slice(-1);
    const next = [...digits];
    next[index] = clean;
    setDigits(next);

    if (clean && index < LENGTH - 1) {
      inputsRef.current[index + 1]?.focus();
    }

    if (next.every((d) => d !== "")) {
      runVerification(next.join(""));
    }
  }

  function handleKeyDown(index, event) {
    if (event.key === "Backspace" && !digits[index] && index > 0) {
      inputsRef.current[index - 1]?.focus();
    }
  }

  async function handleSetPassword(event) {
    event.preventDefault();
    setStatus("saving");
    setErrorMsg(null);

    try {
      await resetPassword({ userId, resetToken: digits.join(""), password: newPassword });
      setStatus("success");
      window.setTimeout(() => navigate("/auth/login?reset=1"), 1200);
    } catch (err) {
      setStatus("password");
      setErrorMsg(
        err.message ||
          { ar: "تعذّر تعيين كلمة المرور الجديدة.", en: "Couldn't set the new password.", ur: "نیا پاس ورڈ محفوظ نہیں ہو سکا۔" }[locale]
      );
    }
  }

  return (
    <AuthShell
      eyebrow={t("otpEyebrow")}
      title={t("otpTitle")}
      subtitle={flow === "reset" ? t("otpResetSub") : t("otpLoginSub")}
    >
      {status === "success" ? (
        <div className="flex flex-col items-center py-10 animate-rise-in">
          <div className="relative size-16 mb-5">
            <div className="absolute inset-0 rounded-full bg-primary/15 animate-ping-slow" />
            <div className="relative size-16 rounded-full bg-primary text-primary-foreground grid place-items-center shadow-glow">
              <Check className="size-7" strokeWidth={2.5} />
            </div>
          </div>
          <p className="font-display text-lg font-bold">{t("verifiedTitle")}</p>
        </div>
      ) : status === "password" || status === "saving" ? (
        <form onSubmit={handleSetPassword} className="space-y-4 animate-rise-in">
          {errorMsg && (
            <div className="flex items-start gap-2.5 rounded-2xl bg-error-tint text-error px-4 py-3 text-sm">
              <AlertCircle className="size-4 shrink-0 mt-0.5" />
              <span>{errorMsg}</span>
            </div>
          )}

          <input
            type="password"
            required
            minLength={6}
            value={newPassword}
            onChange={(e) => setNewPassword(e.target.value)}
            placeholder={t("passwordPh")}
            className="w-full px-5 py-3.5 rounded-2xl bg-stone-50 border border-stone-200 focus:border-primary focus:outline-none focus:ring-2 focus:ring-primary/10 transition-all"
          />

          <button
            type="submit"
            disabled={status === "saving"}
            className="w-full inline-flex items-center justify-center gap-2 bg-primary text-primary-foreground px-6 py-3.5 rounded-2xl font-semibold shadow-glow hover:-translate-y-0.5 transition-transform disabled:opacity-70"
          >
            {status === "saving" ? (
              <Loader2 className="size-4 animate-spin" />
            ) : (
              <ArrowRight className={`size-4 ${dir === "rtl" ? "rotate-180" : ""}`} />
            )}
            {t("sendResetCta")}
          </button>
        </form>
      ) : (
        <>
          {errorMsg && (
            <div className="mb-5 flex items-start gap-2.5 rounded-2xl bg-error-tint text-error px-4 py-3 text-sm">
              <AlertCircle className="size-4 shrink-0 mt-0.5" />
              <span>{errorMsg}</span>
            </div>
          )}

          <div dir="ltr" className="flex justify-between gap-2 mb-6">
            {digits.map((digit, index) => (
              <input
                key={index}
                ref={(el) => (inputsRef.current[index] = el)}
                value={digit}
                onChange={(event) => updateDigit(index, event.target.value)}
                onKeyDown={(event) => handleKeyDown(index, event)}
                disabled={status === "verifying"}
                inputMode="numeric"
                maxLength={1}
                className="size-12 text-center text-lg font-bold rounded-2xl bg-stone-50 border border-stone-200 focus:border-primary focus:outline-none focus:ring-2 focus:ring-primary/10 transition-all"
              />
            ))}
          </div>

          <div className="flex items-center justify-center gap-2 text-sm text-muted-foreground mb-2 h-10">
            {status === "verifying" && (
              <>
                <DammaMark className="size-4 text-primary" spin />
                {t("verifying")}
              </>
            )}
          </div>
        </>
      )}
    </AuthShell>
  );
}
