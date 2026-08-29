import { useEffect } from "react";
import { Link } from "react-router-dom";
import { useI18n } from "../lib/useI18n";
import Reveal, { RevealGroup } from "../components/Reveal";
import {
  Sparkles, Wallet, Headphones, KeyRound, Camera, Briefcase, Smartphone,
  Search, Image as ImageIcon, Gauge, BrainCircuit, Languages, ArrowRight,
  Plane, Building2, GraduationCap, HeartPulse, Ticket, Hotel, Landmark, Cpu,
} from "lucide-react";

function T({ k }) {
  const { t } = useI18n();
  return <>{t(k)}</>;
}

function Landing() {
  const { lang } = useI18n();

  useEffect(() => {
    document.title = "قيا Luqya — Recover what matters, powered by AI";

    const description =
      document.querySelector('meta[name="description"]') ||
      document.head.appendChild(document.createElement("meta"));

    description.setAttribute("name", "description");
    description.setAttribute(
      "content",
      "Bilingual AI lost & found platform. Semantic matching, image understanding, and instant recovery — in Arabic and English."
    );
  }, []);
  return (
    <>
      <Hero />
      <Problem />
      <SemanticDemo />
      <Pipeline />
      <Stats />
      <Features />
      <UseCases />
      <CTA lang={lang} />
    </>
  );
}

/* ------------------------------- HERO ------------------------------- */
function Hero() {
  const { lang } = useI18n();
  const alignRTL = lang === "ar";

  return (
    <section className="relative overflow-hidden pt-16 pb-32 bg-aurora">
      {/* soft grain */}
      <div className="pointer-events-none absolute inset-0 opacity-[0.04] text-primary bg-dots" />

      <div className="max-w-7xl mx-auto px-6 relative">
        <div className="grid lg:grid-cols-2 gap-16 items-center">

          {/* النص */}
          <div
            className={`animate-fade-up ${
              alignRTL ? "lg:order-2 text-right" : "text-left"
            }`}
          >
            {/* Badge */}
            <div className="inline-flex items-center gap-2 px-4 py-1.5 rounded-full bg-primary/5 text-primary text-xs font-semibold mb-8 ring-1 ring-primary/10">
              <span className="size-1.5 rounded-full bg-primary animate-pulse" />
              <T k="heroBadge" />
            </div>

            {/* Title */}
            <h1 className="font-display text-5xl sm:text-6xl lg:text-[5.5rem] font-extrabold leading-[1.05] mb-8 text-balance tracking-tight">
              <T k="heroTitleA" />{" "}

              <span className="text-primary relative inline-block">
                <T k="heroTitleB" />

                <svg
                  className="absolute -bottom-2 left-0 w-full"
                  viewBox="0 0 200 8"
                  preserveAspectRatio="none"
                  aria-hidden
                >
                  <path
                    d="M2 5 Q 100 -2 198 5"
                    stroke="var(--accent)"
                    strokeWidth="2.5"
                    fill="none"
                    strokeLinecap="round"
                  />
                </svg>
              </span>
            </h1>

            {/* Description */}
            <p className="text-lg lg:text-xl text-muted-foreground max-w-xl leading-relaxed mb-10 text-pretty">
              <T k="heroSub" />
            </p>

            {/* Actions */}
            <div
              className={`flex flex-wrap items-center gap-4 ${
                alignRTL ? "justify-end" : "justify-start"
              }`}
            >
              <Link
                to="/report"
                className="inline-flex items-center gap-2 px-8 py-4 bg-primary text-primary-foreground rounded-2xl font-semibold text-base shadow-luxe hover:-translate-y-0.5 transition-transform"
              >
                <Sparkles className="size-4" />
                <T k="ctaReport" />
              </Link>

              <Link
                to="/search"
                className="inline-flex items-center gap-2 px-8 py-4 bg-card border border-border rounded-2xl font-semibold text-base hover:bg-stone-100 transition-colors"
              >
                <T k="navSearch" />
                <ArrowRight
                  className={`size-4 ${alignRTL ? "rotate-180" : ""}`}
                />
              </Link>
            </div>
          </div>

          {/* الرسم — decorative only, hidden on mobile/tablet to save space */}
          <div className="hidden lg:block">
            <ObjectConstellation rtl={alignRTL} />
          </div>

        </div>
      </div>
    </section>
  );
}

function ObjectConstellation({ rtl }) {
  const items = [
    { Icon: Briefcase, label: "Bag", x: "10%", y: "8%", size: "size-24", delay: "0s" },
    { Icon: Wallet, label: "Wallet", x: "55%", y: "0%", size: "size-20", delay: ".4s" },
    { Icon: KeyRound, label: "Keys", x: "72%", y: "42%", size: "size-24", delay: ".8s" },
    { Icon: Camera, label: "Camera", x: "6%", y: "48%", size: "size-28", delay: "1.1s" },
    { Icon: Headphones, label: "Headphones", x: "42%", y: "62%", size: "size-32", delay: "1.5s" },
    { Icon: Smartphone, label: "Phone", x: "78%", y: "78%", size: "size-20", delay: "1.9s" },
  ];
  return (
    <div className={`relative h-[520px] w-full ${rtl ? "lg:order-1" : ""}`}>
      {/* pulse rings */}
      <div className="absolute inset-0 flex items-center justify-center pointer-events-none">
        <div className="relative size-72">
          <div className="absolute inset-0 rounded-full border border-primary/20" />
          <div className="absolute inset-0 rounded-full border border-primary/30 animate-ping-slow" />
          <div className="absolute inset-6 rounded-full border border-dashed border-primary/20 animate-drift" />
          <div className="absolute inset-0 flex items-center justify-center">
            <div className="size-16 rounded-full bg-primary text-primary-foreground grid place-items-center shadow-luxe">
              <BrainCircuit className="size-7" />
            </div>
          </div>
        </div>
      </div>

      {/* neural connecting lines */}
      <svg className="absolute inset-0 w-full h-full pointer-events-none" viewBox="0 0 100 100" preserveAspectRatio="none" aria-hidden>
        {items.map((it, i) => {
          const x1 = parseFloat(it.x);
          const y1 = parseFloat(it.y);
          return (
            <line
              key={i}
              x1={x1 + 6}
              y1={y1 + 6}
              x2={50}
              y2={50}
              stroke="var(--primary)"
              strokeWidth="0.15"
              strokeOpacity="0.35"
              strokeDasharray="0.6 0.6"
            />
          );
        })}
      </svg>

      {items.map((it, i) => (
        <div key={i} className="absolute animate-float" style={{ left: it.x, top: it.y, animationDelay: it.delay }}>
          <div className={`${it.size} bg-card rounded-[1.75rem] shadow-luxe border border-border grid place-items-center relative`}>
            <it.Icon className="size-8 text-primary" strokeWidth={1.5} />
            <span className="absolute -bottom-6 left-1/2 -translate-x-1/2 text-[9px] font-mono uppercase tracking-widest text-muted-foreground">
              {it.label}
            </span>
          </div>
        </div>
      ))}
    </div>
  );
}

/* ---------------------------- PROBLEM ---------------------------- */
function Problem() {
  return (
    <section className="py-24">
      <div className="max-w-5xl mx-auto px-6">
        <Reveal direction="up" className="max-w-xl mb-14">
          <div className="text-[11px] font-mono uppercase tracking-widest text-primary font-bold mb-3">
            <T k="probEyebrow" />
          </div>
          <h2 className="font-display text-3xl lg:text-4xl font-extrabold tracking-tight mb-4">
            <T k="probHeading" />
          </h2>
          <p className="text-muted-foreground text-lg leading-relaxed">
            <T k="probBody" />
          </p>
        </Reveal>

        <div className="grid md:grid-cols-2 gap-5">
          <Reveal direction="left" delay={80} className="rounded-[1.75rem] p-8 border border-border bg-stone-50">
            <div className="flex items-center gap-2.5 mb-6">
              <span className="size-8 rounded-xl bg-error-tint text-error grid place-items-center text-sm font-bold">✕</span>
              <span className="font-bold text-[15px]"><T k="probOldSystem" /></span>
            </div>

            <Snippet labelKey="probLostLabel" textKey="probLostText" />
            <Snippet labelKey="probFoundLabel" textKey="probFoundText" />

            <span className="mt-4 inline-flex items-center rounded-full bg-error-tint text-error px-4 py-2 text-xs font-bold font-mono">
              <T k="probOldResult" />
            </span>
          </Reveal>

          <Reveal direction="right" delay={160} className="rounded-[1.75rem] p-8 border border-primary/25 bg-gradient-to-b from-primary/[0.06] to-transparent">
            <div className="flex items-center gap-2.5 mb-6">
              <span className="size-8 rounded-xl bg-primary/10 text-primary grid place-items-center">
                <svg viewBox="0 0 24 24" className="size-4" fill="none"><path d="M5 13l4 4L19 7" stroke="currentColor" strokeWidth="2.4" strokeLinecap="round" strokeLinejoin="round"/></svg>
              </span>
              <span className="font-bold text-[15px]"><T k="probLuqyaSystem" /></span>
            </div>

            <Snippet labelKey="probLostLabel" textKey="probLostText" />
            <Snippet labelKey="probFoundLabel" textKey="probFoundText" />

            <span className="mt-4 inline-flex items-center rounded-full bg-primary/10 text-primary px-4 py-2 text-xs font-bold font-mono">
              <T k="probLuqyaResult" />
            </span>
          </Reveal>
        </div>
      </div>
    </section>
  );
}

function Snippet({ labelKey, textKey }) {
  return (
    <div className="rounded-2xl bg-card border border-border px-4 py-3.5 mb-3">
      <div className="text-[10px] font-mono uppercase tracking-widest text-muted-foreground/70 mb-1.5">
        <T k={labelKey} />
      </div>
      <p className="text-sm leading-relaxed"><T k={textKey} /></p>
    </div>
  );
}

/* ------------------------- SEMANTIC DEMO ------------------------- */
function SemanticDemo() {
  return (
    <section className="py-24 bg-stone-100/60">
      <div className="max-w-5xl mx-auto px-6">
        <Reveal direction="scale" className="bg-card rounded-[2.5rem] p-8 lg:p-14 shadow-soft border border-border relative overflow-hidden">
          {/* "Neural Engine · v2.0" label removed — internal engineering
              terminology, not meant for end users */}
          <h2 className="text-center font-display text-3xl lg:text-4xl font-bold mb-14 tracking-tight">
            <T k="semTitle" />
          </h2>

          <div className="grid md:grid-cols-[1fr_auto_1fr] items-center gap-8">
            <Reveal direction="left" delay={60} dir="rtl" className="text-right">
              <div className="text-[10px] font-mono text-primary mb-2 uppercase tracking-widest"><T k="semReported" /></div>
              <div className="p-6 rounded-2xl bg-stone-50 border border-stone-200">
                <p className="font-arabic text-lg leading-loose"><T k="semReport" /></p>
              </div>
            </Reveal>

            <Reveal direction="scale" delay={160} className="flex md:flex-col items-center justify-center gap-4 py-4">
              <div className="relative">
                <div className="absolute inset-0 rounded-full bg-primary/20 blur-xl" />
                <div className="relative size-20 rounded-full bg-primary flex items-center justify-center shadow-luxe">
                  <span className="text-white font-bold text-lg">96%</span>
                </div>
              </div>
              <div className="hidden md:block h-10 w-px bg-primary/30" />
              <span className="text-[10px] font-mono text-primary uppercase font-bold tracking-widest"><T k="semMatch" /></span>
            </Reveal>

            <Reveal direction="right" delay={260}>
              <div className="text-[10px] font-mono text-primary mb-2 uppercase tracking-widest"><T k="semFound" /></div>
              <div className="p-6 rounded-2xl bg-stone-50 border border-stone-200">
                <p className="font-arabic text-lg leading-loose"><T k="semFoundText" /></p>
              </div>
            </Reveal>
          </div>

          <Reveal direction="up" delay={320} className="mt-10 p-6 rounded-2xl bg-primary/[0.03] border border-primary/10 text-center">
            <p className="text-sm text-muted-foreground leading-relaxed"><T k="semReason" /></p>
          </Reveal>
        </Reveal>
      </div>
    </section>
  );
}

/* ---------------------------- PIPELINE ---------------------------- */
function Pipeline() {
  const steps = [
    { n: "01", t: "p1t", d: "p1d" },
    { n: "02", t: "p2t", d: "p2d" },
    { n: "03", t: "p3t", d: "p3d" },
    { n: "04", t: "p4t", d: "p4d" },
  ];
  return (
    <section id="how" className="py-28">
      <div className="max-w-7xl mx-auto px-6">
        <Reveal as="h3" direction="up" className="font-display text-3xl lg:text-5xl font-extrabold text-center mb-20 tracking-tight">
          <T k="pipeTitle" />
        </Reveal>
        <div className="grid grid-cols-1 md:grid-cols-4 gap-10 relative">
          <div className="hidden md:block absolute top-6 left-[12%] right-[12%] h-px bg-gradient-to-r from-transparent via-primary/20 to-transparent" />
          <RevealGroup direction="up" stagger={100} baseDelay={80}>
            {steps.map((s) => (
              <div key={s.n} className="group relative">
                <div className="text-5xl font-extrabold text-primary/10 mb-4 group-hover:text-primary/30 transition-colors font-display leading-none">
                  {s.n}
                </div>
                <h4 className="text-lg font-bold mb-2"><T k={s.t} /></h4>
                <p className="text-sm text-muted-foreground leading-relaxed"><T k={s.d} /></p>
              </div>
            ))}
          </RevealGroup>
        </div>
      </div>
    </section>
  );
}

/* ---------------------------- FEATURES ---------------------------- */
function Features() {
  const feats = [
    { Icon: BrainCircuit, t: "f1t", d: "f1d" },
    { Icon: ImageIcon, t: "f2t", d: "f2d" },
    { Icon: Search, t: "f3t", d: "f3d" },
    { Icon: Gauge, t: "f4t", d: "f4d" },
    { Icon: Sparkles, t: "f5t", d: "f5d" },
    { Icon: Languages, t: "f6t", d: "f6d" },
  ];
  return (
    <section id="features" className="py-28 bg-stone-100/60">
      <div className="max-w-7xl mx-auto px-6">
        <Reveal direction="up" className="max-w-2xl mb-16">
          <h3 className="font-display text-3xl lg:text-5xl font-extrabold mb-4 tracking-tight">
            <T k="featTitle" />
          </h3>
          <p className="text-muted-foreground text-lg text-pretty"><T k="featSub" /></p>
        </Reveal>
        <div className="grid md:grid-cols-2 lg:grid-cols-3 gap-5">
          {feats.map((f, i) => (
            <Reveal key={i} direction="up" delay={80 + Math.min(i, 2) * 90} className="group p-8 rounded-3xl bg-card border border-border hover:shadow-luxe hover:-translate-y-1 transition-all duration-500">
              <div className="size-12 rounded-2xl bg-primary/5 text-primary grid place-items-center mb-6 group-hover:bg-primary group-hover:text-primary-foreground transition-colors">
                <f.Icon className="size-5" strokeWidth={1.75} />
              </div>
              <h4 className="text-lg font-bold mb-2"><T k={f.t} /></h4>
              <p className="text-sm text-muted-foreground leading-relaxed"><T k={f.d} /></p>
            </Reveal>
          ))}
        </div>
      </div>
    </section>
  );
}

/* ---------------------------- STATS ---------------------------- */
/* Numbers are deliberately modest and defensible for a pre-scale product:
   - 95% / 10s mirror the demo/example figures already used elsewhere in the
     UI (SemanticDemo's 96% match, probLuqyaResult's 94%), framed explicitly
     as testing-environment figures rather than an audited production stat.
   - 3 languages is the actual number of locale files in this project
     (ar, en, ur) — verified, not assumed.
   - "2 search modes" reflects the two verified input modes described in
     searchByImageHint (text and image, usable together).
   No claim here implies real user counts, cities, or recovered-item totals. */
function Stats() {
  const stats = [
    { n: "95%", l: "statAccuracy" },
    { n: "10s", l: "statSpeed" },
    { n: "3", l: "statLanguages" },
    { n: "2", l: "statSearchModes" },
  ];
  return (
    <section className="bg-primary py-20 text-primary-foreground overflow-hidden relative">
      <div className="absolute inset-0 opacity-[0.06] bg-[radial-gradient(circle_at_center,_white_1px,_transparent_1px)] bg-[size:32px_32px]" />
      <div className="absolute -top-24 -end-24 size-96 rounded-full bg-accent/10 blur-3xl" />
      <div className="max-w-7xl mx-auto px-6 grid grid-cols-2 md:grid-cols-4 gap-10 relative z-10">
        <RevealGroup direction="up" stagger={90}>
          {stats.map((s) => (
            <div key={s.n} className="text-center md:text-start">
              <div className="font-display text-4xl lg:text-6xl font-extrabold mb-2 tracking-tight">{s.n}</div>
              <div className="text-[11px] uppercase tracking-widest text-white/60 font-medium"><T k={s.l} /></div>
            </div>
          ))}
        </RevealGroup>
      </div>
    </section>
  );
}

/* ---------------------------- USE CASES ---------------------------- */
function UseCases() {
  const cases = [
    { Icon: Plane, l: "uAirport" },
    { Icon: Building2, l: "uMall" },
    { Icon: HeartPulse, l: "uHospital" },
    { Icon: GraduationCap, l: "uUni" },
    { Icon: Ticket, l: "uEvent" },
    { Icon: Hotel, l: "uHotel" },
    { Icon: Landmark, l: "uGov" },
    { Icon: Cpu, l: "uCity" },
  ];
  return (
    <section className="py-28">
      <div className="max-w-7xl mx-auto px-6">
        <Reveal direction="up" className="max-w-2xl mb-16">
          <h3 className="font-display text-3xl lg:text-5xl font-extrabold mb-4 tracking-tight">
            <T k="useTitle" />
          </h3>
          <p className="text-muted-foreground text-lg text-pretty"><T k="useSub" /></p>
        </Reveal>
        <div className="grid grid-cols-2 md:grid-cols-4 gap-4">
          {cases.map((c, i) => (
            <Reveal key={i} direction="scale" delay={i * 70} className="group aspect-square rounded-3xl border border-border bg-card p-6 flex flex-col justify-between hover:bg-primary hover:text-primary-foreground hover:border-primary transition-colors duration-500 cursor-default">
              <c.Icon className="size-7" strokeWidth={1.5} />
              <div className="font-semibold text-base"><T k={c.l} /></div>
            </Reveal>
          ))}
        </div>
      </div>
    </section>
  );
}

function CTA({ lang }) {
  return (
    <section className="py-28">
      <div className="max-w-5xl mx-auto px-6">
        <Reveal direction="scale" duration={700} className="relative rounded-[2.5rem] overflow-hidden bg-primary text-primary-foreground p-12 lg:p-20 text-center shadow-luxe">
          <div className="absolute inset-0 opacity-10 bg-[radial-gradient(circle_at_20%_20%,_var(--accent)_0,_transparent_50%)]" />
          <div className="absolute inset-0 opacity-10 bg-[radial-gradient(circle_at_80%_80%,_white_0,_transparent_50%)]" />
          <h3 className="relative font-display text-3xl lg:text-5xl font-extrabold mb-6 tracking-tight text-balance">
            {lang === "ar" ? "ابدأ استرداد ما فقدته اليوم." : "Start recovering what you've lost, today."}
          </h3>
          <div className="relative flex flex-wrap justify-center gap-4">
            <Link to="/report" className="inline-flex items-center gap-2 bg-white text-primary px-8 py-4 rounded-2xl font-bold text-base hover:bg-accent hover:text-accent-foreground transition-colors">
              <Sparkles className="size-4" />
              {lang === "ar" ? "بلّغ الآن" : "Report now"}
            </Link>
            <Link to="/browse" className="inline-flex items-center gap-2 border border-white/25 px-8 py-4 rounded-2xl font-bold text-base hover:bg-white/10 transition-colors">
              {lang === "ar" ? "تصفح البلاغات" : "Browse reports"}
            </Link>
          </div>
        </Reveal>
      </div>
    </section>
  );
}

export default Landing;