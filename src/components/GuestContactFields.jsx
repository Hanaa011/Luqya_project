import { User, Phone, Mail, ShieldCheck } from "lucide-react";
import { PreferredContactType, preferredContactLabelKey } from "../api/enums";

/**
 * Shared between ReportLost and ReportFound so the guest-contact UI (and
 * its validation/privacy copy) exists in exactly one place. Only rendered
 * when the visitor isn't authenticated — CreateReportDto ignores these
 * fields server-side for logged-in users.
 */
export default function GuestContactFields({ t, tone = "primary", values, onChange, phoneError, emailError }) {
  const ring = tone === "accent" ? "focus:border-accent focus:ring-accent/15" : "focus:border-primary focus:ring-primary/10";
  const pillActive = tone === "accent" ? "bg-accent text-accent-foreground" : "bg-primary text-primary-foreground";

  return (
    <div className="space-y-5 rounded-[1.5rem] border border-dashed border-stone-200 p-6">
      <div className="flex items-center gap-2">
        <User className="size-4 text-muted-foreground" />
        <div>
          <div className="text-sm font-semibold">{t("guestInfoTitle")}</div>
          <div className="text-xs text-muted-foreground">{t("guestInfoSub")}</div>
        </div>
      </div>

      <div className="grid sm:grid-cols-2 gap-4">
        <div>
          <label className="text-xs font-semibold flex items-center gap-1.5 mb-2">
            <User className="size-3.5 text-muted-foreground" />
            {t("fldFullName")}
          </label>
          <input
            type="text"
            required
            value={values.reporterName}
            onChange={(e) => onChange("reporterName", e.target.value)}
            placeholder={t("fldFullNamePh")}
            className={`w-full px-4 py-3 rounded-xl bg-card border border-stone-200 focus:outline-none focus:ring-2 transition-all text-sm ${ring}`}
          />
        </div>

        <div>
          <label className="text-xs font-semibold flex items-center gap-1.5 mb-2">
            <Phone className="size-3.5 text-muted-foreground" />
            {t("fldMobile")}
          </label>
          <input
            type="tel"
            required
            dir="ltr"
            value={values.reporterPhone}
            onChange={(e) => onChange("reporterPhone", e.target.value)}
            placeholder={t("fldMobilePh")}
            className={`w-full px-4 py-3 rounded-xl bg-card border text-start transition-all text-sm focus:outline-none focus:ring-2 ${ring} ${
              phoneError ? "border-error" : "border-stone-200"
            }`}
          />
          {phoneError && <p className="text-xs text-error mt-1.5">{t("fldMobileInvalid")}</p>}
        </div>
      </div>

      <div>
        <label className="text-xs font-semibold flex items-center gap-1.5 mb-2">
          <Mail className="size-3.5 text-muted-foreground" />
          {t("fldEmail")}
        </label>
        <input
          type="email"
          required
          dir="ltr"
          value={values.reporterEmail}
          onChange={(e) => onChange("reporterEmail", e.target.value)}
          placeholder={t("fldEmailPh")}
          className={`w-full px-4 py-3 rounded-xl bg-card border text-start transition-all text-sm focus:outline-none focus:ring-2 ${ring} ${
            emailError ? "border-error" : "border-stone-200"
          }`}
        />
        {emailError && <p className="text-xs text-error mt-1.5">{t("fldEmailInvalid")}</p>}
      </div>

      <div>
        <label className="text-xs font-semibold block mb-2">{t("preferredContactLabel")}</label>
        <div className="inline-flex p-1 bg-stone-100 rounded-full gap-1">
          {[PreferredContactType.PHONE, PreferredContactType.EMAIL, PreferredContactType.WHATSAPP].map((option) => (
            <button
              key={option}
              type="button"
              onClick={() => onChange("preferredContact", option)}
              className={`px-3.5 py-1.5 rounded-full text-xs font-semibold transition-all ${
                values.preferredContact === option ? pillActive : "text-foreground/60 hover:text-foreground"
              }`}
            >
              {t(preferredContactLabelKey(option))}
            </button>
          ))}
        </div>
      </div>

      <div className="flex items-start gap-2 text-xs text-muted-foreground pt-1 border-t border-stone-100">
        <ShieldCheck className="size-3.5 shrink-0 mt-0.5" />
        <span>{t("privacyNotice")}</span>
      </div>
    </div>
  );
}
