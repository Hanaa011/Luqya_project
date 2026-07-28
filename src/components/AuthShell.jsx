import { Link } from "react-router-dom";
import { useI18n } from "../lib/useI18n";
import Logo from "./Logo";

/**
 * The branded panel is pinned to a fixed physical side regardless of
 * language — flipping a hero illustration whenever someone switches
 * Arabic/English adds motion without meaning, so only the form half
 * follows text direction. That's the "intelligent adaptation, not
 * mirroring" the brief asks for.
 *
 * The panel's motion is entirely ambient — soft light, a huge, barely-
 * there echo of the logo's own waves, and a slow glass sheen — so nothing
 * ever sits on top of the words next to it.
 */
export default function AuthShell({ eyebrow, title, subtitle, children }) {
  const { t, dir, meta } = useI18n();

  return (
    <div className="min-h-dvh grid lg:grid-cols-2" dir="ltr">
      <div className="relative hidden lg:flex flex-col justify-between overflow-hidden bg-[#0B1614] text-[#f3f1ea] p-12">
        <div className="absolute inset-0 bg-aurora-ink" />

        {/* Ambient light — soft, slow, never sharp enough to compete with text */}
        <div className="absolute -top-24 -start-24 size-96 rounded-full bg-primary/20 blur-3xl animate-glow-pulse" />
        <div className="absolute bottom-0 end-0 size-80 rounded-full bg-accent/14 blur-3xl" />

        {/* A huge, near-invisible echo of the logo's own waves, anchored
            to the corner as pure texture rather than a floating object */}
        <svg
          viewBox="0 0 100 100"
          className="absolute -bottom-16 -end-16 size-[30rem] opacity-[0.07] pointer-events-none"
          aria-hidden="true"
        >
          <path d="M61.95 58.37 A34 34 0 0 1 41.63 38.05" fill="none" stroke="#E7F3F1" strokeWidth="2.2" strokeLinecap="round" />
          <path d="M50.67 62.48 A22 22 0 0 1 37.52 49.33" fill="none" stroke="#12968A" strokeWidth="2.6" strokeLinecap="round" />
        </svg>

        {/* A slow glass sheen sweeping across the panel */}
        <div
          className="absolute inset-y-0 start-0 w-1/2 opacity-[0.05] pointer-events-none animate-sheen"
          style={{
            background: "linear-gradient(100deg, transparent, white, transparent)",
          }}
        />

        <div className="relative">
          <Link to="/">
            <Logo tone="night" wordmark className="size-9" wordmarkClassName="text-3xl" glow />
          </Link>
        </div>

        <div className="relative">
          <p className={`font-display text-4xl font-bold leading-tight max-w-sm ${meta.code === "ur" ? "font-urdu !text-3xl" : ""}`}>
            {t("authTaglineA")} <span className="text-primary-bright">{t("authTaglineB")}</span> {t("authTaglineC")}
          </p>

          <p className={`mt-4 text-white/50 max-w-xs text-sm leading-relaxed ${meta.code === "ur" ? "font-urdu" : ""}`}>
            {t("authTaglineSub")}
          </p>
        </div>

        <div className="relative text-[10px] font-mono uppercase tracking-widest text-white/30">
          {t("authBadge")}
        </div>
      </div>

      <div className="flex items-start lg:items-center justify-center px-6 py-16 bg-background">
        <div className="w-full max-w-sm" dir={dir}>
          <Link to="/" className="lg:hidden inline-block mb-10">
            <Logo tone="default" wordmark className="size-8" wordmarkClassName="text-2xl" />
          </Link>

          {eyebrow && (
            <div className="text-[11px] font-mono uppercase tracking-widest text-primary font-bold mb-3">
              {eyebrow}
            </div>
          )}

          <h1 className={`font-display text-3xl font-extrabold tracking-tight mb-2 ${meta.code === "ur" ? "font-urdu !text-2xl" : ""}`}>
            {title}
          </h1>

          {subtitle && (
            <p className="text-muted-foreground text-sm mb-8">{subtitle}</p>
          )}

          {!subtitle && <div className="mb-8" />}

          {children}
        </div>
      </div>
    </div>
  );
}
