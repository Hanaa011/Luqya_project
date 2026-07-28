import { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import { Send, Sparkles, MessageCircle, ShieldCheck, PackageCheck, ArrowLeft, Check } from "lucide-react";
import { useI18n } from "../../lib/useI18n";
import { translate } from "../../lib/translate";
import { LOCALES } from "../../i18n/locales";
import DammaMark from "../../components/DammaMark";

const TEMPLATES = [
  { key: "notifMatch", Icon: Sparkles },
  { key: "notifOwnerReply", Icon: MessageCircle },
  { key: "notifVerified", Icon: ShieldCheck },
  { key: "notifScheduled", Icon: PackageCheck },
];

const CHANNELS = ["push", "sms", "email"];

/**
 * The recipient's saved app-language preference and the language a staff
 * member chooses here are two independent settings. This screen exists to
 * make that split concrete: everything above the divider uses the admin's
 * *own* UI language (via useI18n's t()); everything in the preview uses
 * translate(localeCode, key) directly — a delivery language the admin
 * picks, that has nothing to do with which language they're browsing in.
 */
export default function NotificationComposer() {
  const { t, dir, locale: adminLocale } = useI18n();
  const [mode, setMode] = useState("auto"); // auto | force
  const [forceLocale, setForceLocale] = useState("en");
  const [templateKey, setTemplateKey] = useState(TEMPLATES[0].key);
  const [channel, setChannel] = useState("push");
  const [sent, setSent] = useState(false);

  useEffect(() => {
    document.title = "Notification console — Luqya";
  }, []);

  function handleSend(event) {
    event.preventDefault();
    setSent(true);
    window.setTimeout(() => setSent(false), 2600);
  }

  return (
    <section className="py-16 lg:py-24">
      <div className="max-w-4xl mx-auto px-6">
        <Link
          to="/"
          className="inline-flex items-center gap-2 text-sm font-semibold text-muted-foreground hover:text-primary mb-8 transition-colors"
        >
          <ArrowLeft className={`size-4 ${dir === "rtl" ? "rotate-180" : ""}`} />
          {t("backLabel")}
        </Link>

        <div className="mb-10">
          <div className="inline-flex items-center gap-2 text-[11px] font-mono uppercase tracking-widest text-primary font-bold mb-3">
            <DammaMark className="size-3.5" />
            {t("adminNotifyEyebrow")}
          </div>
          <h1 className="font-display text-3xl lg:text-4xl font-extrabold tracking-tight mb-3">
            {t("adminNotifyTitle")}
          </h1>
          <p className="text-muted-foreground text-lg max-w-2xl">{t("adminNotifySub")}</p>
        </div>

        <form onSubmit={handleSend} className="grid lg:grid-cols-[1.1fr_0.9fr] gap-6">
          <div className="bg-card border border-border rounded-[2rem] p-8 space-y-7 shadow-soft">
            <div>
              <div className="text-sm font-semibold mb-3">{t("adminNotifyMessage")}</div>
              <div className="grid sm:grid-cols-2 gap-2.5">
                {TEMPLATES.map(({ key, Icon }) => (
                  <button
                    key={key}
                    type="button"
                    onClick={() => setTemplateKey(key)}
                    className={`flex items-center gap-2.5 rounded-2xl border px-4 py-3 text-start text-sm font-medium transition-all ${
                      templateKey === key
                        ? "border-primary bg-primary/5 text-primary"
                        : "border-border hover:bg-stone-50"
                    }`}
                  >
                    <Icon className="size-4 shrink-0" />
                    <span className="truncate">{t(key)}</span>
                  </button>
                ))}
              </div>
            </div>

            <div>
              <div className="text-sm font-semibold mb-3">{t("adminNotifyChannel")}</div>
              <div className="inline-flex p-1.5 bg-stone-100 rounded-full gap-1">
                {CHANNELS.map((c) => (
                  <button
                    key={c}
                    type="button"
                    onClick={() => setChannel(c)}
                    className={`px-4 py-2 rounded-full text-xs font-semibold uppercase tracking-wide transition-all ${
                      channel === c
                        ? "bg-primary text-primary-foreground shadow-soft"
                        : "text-foreground/60 hover:text-foreground"
                    }`}
                  >
                    {c}
                  </button>
                ))}
              </div>
            </div>

            <div>
              <div className="text-sm font-semibold mb-3">{t("adminNotifyRecipientLang")}</div>

              <div className="space-y-2.5">
                <label
                  className={`flex items-center gap-3 rounded-2xl border px-4 py-3.5 cursor-pointer transition-all ${
                    mode === "auto" ? "border-primary bg-primary/5" : "border-border hover:bg-stone-50"
                  }`}
                >
                  <input
                    type="radio"
                    name="lang-mode"
                    checked={mode === "auto"}
                    onChange={() => setMode("auto")}
                    className="accent-current text-primary"
                  />
                  <span className="text-sm">
                    <span className="font-semibold block">{t("adminNotifyAuto")}</span>
                  </span>
                </label>

                <label
                  className={`flex items-center gap-3 rounded-2xl border px-4 py-3.5 cursor-pointer transition-all ${
                    mode === "force" ? "border-primary bg-primary/5" : "border-border hover:bg-stone-50"
                  }`}
                >
                  <input
                    type="radio"
                    name="lang-mode"
                    checked={mode === "force"}
                    onChange={() => setMode("force")}
                    className="accent-current text-primary"
                  />
                  <span className="text-sm font-semibold">{t("adminNotifyForce")}</span>
                </label>

                {mode === "force" && (
                  <div className="flex gap-2 ps-9 pt-1 animate-fade-up">
                    {LOCALES.map((l) => (
                      <button
                        key={l.code}
                        type="button"
                        onClick={() => setForceLocale(l.code)}
                        className={`flex-1 flex flex-col items-center gap-1 rounded-xl border py-2.5 text-xs font-semibold transition-all ${
                          forceLocale === l.code
                            ? "border-primary bg-primary/5 text-primary"
                            : "border-border hover:bg-stone-50"
                        }`}
                      >
                        <span className="text-base leading-none">{l.flag}</span>
                        {l.englishName}
                      </button>
                    ))}
                  </div>
                )}
              </div>
            </div>

            <button
              type="submit"
              className="w-full inline-flex items-center justify-center gap-2 bg-primary text-primary-foreground px-6 py-3.5 rounded-2xl font-semibold shadow-glow hover:-translate-y-0.5 transition-transform"
            >
              {sent ? <Check className="size-4" /> : <Send className="size-4" />}
              {sent ? t("adminNotifySent") : t("adminNotifySend")}
            </button>
          </div>

          <div className="space-y-3">
            <div className="text-[11px] font-mono uppercase tracking-widest text-muted-foreground font-bold px-1">
              {t("adminNotifyPreview")}
            </div>

            {(mode === "force" ? [forceLocale] : LOCALES.map((l) => l.code)).map((code) => {
              const meta = LOCALES.find((l) => l.code === code);
              return (
                <div
                  key={code}
                  dir={meta.dir}
                  className={`rounded-2xl border border-border bg-card p-5 shadow-soft ${meta.font}`}
                >
                  <div className="flex items-center gap-2 mb-3" dir="ltr">
                    <span className="text-base leading-none">{meta.flag}</span>
                    <span className="text-[10px] font-mono uppercase tracking-widest text-muted-foreground">
                      {meta.englishName}
                      {mode === "auto" && (
                        <>
                          {" "}
                          ·{" "}
                          {adminLocale === "ar"
                            ? "مستلم محفوظ بهذه اللغة"
                            : adminLocale === "ur"
                            ? "اس زبان کے وصول کنندہ"
                            : "recipient saved this language"}
                        </>
                      )}
                    </span>
                  </div>

                  <p className="text-sm leading-relaxed">{translate(code, templateKey)}</p>

                  <div className="mt-3 pt-3 border-t border-border text-[10px] font-mono uppercase tracking-widest text-muted-foreground/70">
                    {channel} · Luqya
                  </div>
                </div>
              );
            })}
          </div>
        </form>
      </div>
    </section>
  );
}
