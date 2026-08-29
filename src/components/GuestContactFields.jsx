import { User, Phone, Mail, ShieldCheck } from "lucide-react";

export default function GuestContactFields({
  t,
  dir,
  tone = "primary",
  values,
  onChange,
  phoneError,
  emailError,
}) {
  const focusTone =
    tone === "accent"
      ? "focus:border-accent/70 focus:ring-accent/10"
      : "focus:border-primary/60 focus:ring-primary/10";

  const iconTone =
    tone === "accent"
      ? "bg-accent/[0.08] text-accent"
      : "bg-primary/[0.07] text-primary";

  const inputBase = `
    w-full rounded-xl border bg-background
    px-4 py-3.5 text-sm text-foreground
    outline-none transition-all duration-200
    placeholder:text-muted-foreground/55
    focus:ring-2
  `;

  return (
    <section className="overflow-hidden rounded-[1.5rem] border border-border/80 bg-card">
      {/* Header */}
      <div className="flex items-start gap-3 border-b border-border/70 px-5 py-4 sm:px-6">
        <span
          className={`mt-0.5 grid size-9 shrink-0 place-items-center rounded-xl ${iconTone}`}
        >
          <User className="size-4" strokeWidth={1.8} />
        </span>

        <div className="min-w-0">
          <h2 className="text-sm font-bold text-foreground sm:text-base">
            {t("guestInfoTitle")}
          </h2>
          <p className="mt-1 max-w-2xl text-xs leading-5 text-muted-foreground">
            {t("guestInfoSub")}
          </p>
        </div>
      </div>

      {/* Fields */}
      <div className="space-y-5 px-5 py-5 sm:px-6 sm:py-6">
        <div className="grid gap-5 md:grid-cols-2">
          {/* Full name */}
          <div>
            <label className="mb-2 flex items-center gap-2 text-xs font-semibold text-foreground/80">
              <User className="size-3.5 text-muted-foreground" strokeWidth={1.8} />
              {t("fldFullName")}
            </label>

            <input
              type="text"
              required
              value={values.reporterName}
              onChange={(e) => onChange("reporterName", e.target.value)}
              placeholder={t("fldFullNamePh")}
              className={`${inputBase} border-border/90 ${focusTone}`}
            />
          </div>

          {/* Mobile */}
          <div>
            <label className="mb-2 flex items-center gap-2 text-xs font-semibold text-foreground/80">
              <Phone className="size-3.5 text-muted-foreground" strokeWidth={1.8} />
              {t("fldMobile")}
            </label>

            <input
              type="tel"
              required
              inputMode="tel"
              dir="ltr"
              value={values.reporterPhone}
              onChange={(e) => onChange("reporterPhone", e.target.value)}
              placeholder={t("fldMobilePh")}
              aria-invalid={phoneError ? "true" : "false"}
              className={`${inputBase} text-left ${
                phoneError
                  ? "border-error/70 focus:border-error focus:ring-error/10"
                  : `border-border/90 ${focusTone}`
              }`}
            />

            {phoneError && (
              <p
                className={`mt-1.5 text-[11px] font-medium text-error ${
                  dir === "rtl" ? "text-right" : "text-left"
                }`}
              >
                {t("fldMobileInvalid")}
              </p>
            )}
          </div>
        </div>

        {/* Email */}
        <div>
          <label className="mb-2 flex items-center gap-2 text-xs font-semibold text-foreground/80">
            <Mail className="size-3.5 text-muted-foreground" strokeWidth={1.8} />
            {t("emailPh")}
          </label>

          <input
            type="email"
            required
            dir="ltr"
            value={values.reporterEmail}
            onChange={(e) => onChange("reporterEmail", e.target.value)}
            placeholder="example@email.com"
            aria-invalid={emailError ? "true" : "false"}
            className={`${inputBase} ${
              dir === "rtl"
                ? "text-right placeholder:text-right"
                : "text-left placeholder:text-left"
            } ${
              emailError
                ? "border-error/70 focus:border-error focus:ring-error/10"
                : `border-border/90 ${focusTone}`
            }`}
          />

          {emailError && (
            <p
              className={`mt-1.5 text-[11px] font-medium text-error ${
                dir === "rtl" ? "text-right" : "text-left"
              }`}
            >
              {dir === "rtl"
                ? "أدخل بريدًا إلكترونيًا صحيحًا"
                : "Enter a valid email address"}
            </p>
          )}
        </div>

        {/* Privacy note */}
        <div className="flex items-start gap-2.5 rounded-xl bg-stone-50/70 px-3.5 py-3 text-[11px] leading-5 text-muted-foreground">
          <ShieldCheck
            className={`mt-0.5 size-3.5 shrink-0 ${
              tone === "accent" ? "text-accent" : "text-primary"
            }`}
            strokeWidth={1.8}
          />
          <span>{t("privacyNotice")}</span>
        </div>
      </div>
    </section>
  );
}
