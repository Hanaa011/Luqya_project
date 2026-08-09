import { Link } from "react-router-dom";
import { LifeBuoy, KeyRound, MailX, CircleAlert, ArrowLeft } from "lucide-react";
import AuthShell from "../../components/AuthShell";
import { useI18n } from "../../lib/useI18n";

// This project has no email/SMS/push delivery and no OTP mechanism — the
// previous "send a verification code" flow could never actually deliver
// anything, so it was a broken promise from the user's perspective. This
// page no longer pretends otherwise: it's a genuine, honest hand-off to
// human support instead of a fake automated flow.
//
// NOTE for whoever owns this: SUPPORT_EMAIL below is a placeholder
// (support@luqya.app), not a verified/monitored address — replace it with
// the team's real support contact before this ships.
const SUPPORT_EMAIL = "support@luqya.app";

export default function ForgotPassword() {
  const { t, dir } = useI18n();

  return (
    <AuthShell
      eyebrow={t("forgotEyebrow")}
      title={t("forgotTitle")}
      subtitle={t("forgotSub")}
    >
      <div className="size-12 rounded-2xl bg-primary/10 text-primary grid place-items-center mb-6">
        <KeyRound className="size-5" />
      </div>

      <div className="rounded-2xl border border-border bg-stone-50 p-5 mb-6">
        <h2 className="text-sm font-bold mb-3">{t("recoveryCardTitle")}</h2>
        <ul className="space-y-2 text-sm text-muted-foreground">
          <li className="flex items-center gap-2.5">
            <KeyRound className="size-3.5 shrink-0 text-primary" />
            {t("recoveryReasonPassword")}
          </li>
          <li className="flex items-center gap-2.5">
            <MailX className="size-3.5 shrink-0 text-primary" />
            {t("recoveryReasonEmail")}
          </li>
          <li className="flex items-center gap-2.5">
            <CircleAlert className="size-3.5 shrink-0 text-primary" />
            {t("recoveryReasonOther")}
          </li>
        </ul>
        <p className="text-xs text-muted-foreground mt-4 pt-4 border-t border-border">
          {t("recoveryCardNote")}
        </p>
      </div>

      <a
        href={`mailto:${SUPPORT_EMAIL}`}
        className="w-full inline-flex items-center justify-center gap-2 bg-primary text-primary-foreground px-6 py-3.5 rounded-2xl font-semibold shadow-glow hover:-translate-y-0.5 transition-transform"
      >
        <LifeBuoy className="size-4" />
        {t("contactSupportCta")}
      </a>

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
