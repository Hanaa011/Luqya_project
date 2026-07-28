import { useEffect } from "react";
import { Link } from "react-router-dom";
import { Frown, HeartHandshake, ArrowRight, ArrowLeft } from "lucide-react";
import { useI18n } from "../lib/useI18n";
import DammaMark from "../components/DammaMark";

export default function ReportChoice() {
  const { t, dir } = useI18n();
  const Arrow = dir === "rtl" ? ArrowLeft : ArrowRight;

  useEffect(() => {
    document.title = "Report — Luqya";
  }, []);

  return (
    <section className="py-16 lg:py-28">
      <div className="max-w-4xl mx-auto px-6 text-center">
        <div className="inline-flex items-center gap-2 text-[11px] font-mono uppercase tracking-widest text-primary font-bold mb-4">
          <DammaMark className="size-3.5" />
          {t("choiceEyebrow")}
        </div>

        <h1 className="font-display text-4xl lg:text-5xl font-extrabold tracking-tight mb-4">
          {t("choiceTitle")}
        </h1>

        <p className="text-muted-foreground text-lg mb-14 max-w-lg mx-auto">
          {t("choiceSub")}
        </p>

        <div className="grid sm:grid-cols-2 gap-6 text-start">
          <Link
            to="/report/lost"
            className="group relative rounded-[2rem] border border-border bg-card p-8 lg:p-10 shadow-soft hover:shadow-luxe hover:-translate-y-1 transition-all overflow-hidden"
          >
            <div className="absolute -top-10 -end-10 size-40 rounded-full bg-primary/10 blur-3xl group-hover:bg-primary/15 transition-colors" />

            <div className="relative">
              <div className="size-14 rounded-2xl bg-primary/10 text-primary grid place-items-center mb-6">
                <Frown className="size-6" strokeWidth={1.5} />
              </div>

              <h2 className="font-display text-2xl font-bold mb-2">
                {t("choiceLostTitle")}
              </h2>

              <p className="text-sm text-muted-foreground leading-relaxed mb-8">
                {t("choiceLostDesc")}
              </p>

              <span className="inline-flex items-center gap-2 text-sm font-semibold text-primary">
                {t("choiceLostCta")}
                <Arrow className="size-4 group-hover:translate-x-1 rtl:group-hover:-translate-x-1 transition-transform" />
              </span>
            </div>
          </Link>

          <Link
            to="/report/found"
            className="group relative rounded-[2rem] border border-border bg-card p-8 lg:p-10 shadow-soft hover:shadow-luxe hover:-translate-y-1 transition-all overflow-hidden"
          >
            <div className="absolute -top-10 -end-10 size-40 rounded-full bg-accent/15 blur-3xl group-hover:bg-accent/25 transition-colors" />

            <div className="relative">
              <div className="size-14 rounded-2xl bg-accent/15 text-accent-foreground grid place-items-center mb-6">
                <HeartHandshake className="size-6" strokeWidth={1.5} />
              </div>

              <h2 className="font-display text-2xl font-bold mb-2">
                {t("choiceFoundTitle")}
              </h2>

              <p className="text-sm text-muted-foreground leading-relaxed mb-8">
                {t("choiceFoundDesc")}
              </p>

              <span className="inline-flex items-center gap-2 text-sm font-semibold text-accent-foreground">
                {t("choiceFoundCta")}
                <Arrow className="size-4 group-hover:translate-x-1 rtl:group-hover:-translate-x-1 transition-transform" />
              </span>
            </div>
          </Link>
        </div>
      </div>
    </section>
  );
}
