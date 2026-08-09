import { useEffect } from "react";
import { Link } from "react-router-dom";
import {
  PackageSearch,
  PackageCheck,
  ArrowRight,
  ArrowLeft,
  ShieldCheck,
} from "lucide-react";

import { useI18n } from "../lib/useI18n";
import DammaMark from "../components/DammaMark";

export default function ReportChoice() {
  const { t, dir } = useI18n();
  const isRtl = dir === "rtl";
  const Arrow = isRtl ? ArrowLeft : ArrowRight;

  useEffect(() => {
    document.title = "Report — Luqya";
  }, []);

  const choices = [
    {
      number: "01",
      to: "/report/lost",
      title: t("choiceLostTitle"),
      description: t("choiceLostDesc"),
      cta: t("choiceLostCta"),
      Icon: PackageSearch,
      accent: "primary",
    },
    {
      number: "02",
      to: "/report/found",
      title: t("choiceFoundTitle"),
      description: t("choiceFoundDesc"),
      cta: t("choiceFoundCta"),
      Icon: PackageCheck,
      accent: "warm",
    },
  ];

  return (
    <section className="relative overflow-hidden py-14 sm:py-20 lg:py-24">
      {/* خلفية هادئة */}
      <div className="pointer-events-none absolute inset-0 bg-aurora opacity-50" />
      <div className="pointer-events-none absolute inset-0 bg-dots opacity-[0.025]" />

      <div className="relative mx-auto max-w-5xl px-4 sm:px-6">
        {/* رأس الصفحة */}
        <header className="mx-auto mb-10 max-w-2xl text-center sm:mb-14">
          <div className="mb-4 inline-flex items-center gap-2 rounded-full border border-primary/10 bg-primary/[0.03] px-3.5 py-1.5 text-[11px] font-medium text-primary/90">
          <DammaMark className="size-3" />
          {t("choiceEyebrow")}
        </div>
          <h1 className="font-display text-4xl font-extrabold tracking-tight text-foreground sm:text-5xl lg:text-6xl">
            {t("choiceTitle")}
          </h1>

          <p className="mx-auto mt-4 max-w-xl text-base leading-8 text-muted-foreground sm:text-lg">
            {t("choiceSub")}
          </p>
        </header>

        {/* خيارات البلاغ */}
        <div className="grid gap-5 md:grid-cols-2">
          {choices.map(
            ({
              number,
              to,
              title,
              description,
              cta,
              Icon,
              accent,
            }) => {
              const isPrimary = accent === "primary";

              return (
                <Link
                  key={to}
                  to={to}
                  aria-label={title}
                  className={`
                    group relative isolate min-h-[290px] overflow-hidden
                    rounded-[1.75rem] border bg-card/90 p-6
                    shadow-[0_8px_30px_rgba(15,23,42,0.045)]
                    backdrop-blur-sm
                    transition-all duration-300
                    hover:-translate-y-1
                    hover:shadow-[0_18px_50px_rgba(15,23,42,0.09)]
                    focus-visible:outline-none
                    focus-visible:ring-2
                    focus-visible:ring-primary/25
                    sm:p-8
                    ${
                      isPrimary
                        ? "border-primary/15 hover:border-primary/30"
                        : "border-accent/25 hover:border-accent/45"
                    }
                  `}
                >
                  {/* توهج خفيف */}
                  <div
                    className={`
                      pointer-events-none absolute -end-20 -top-24 -z-10
                      size-56 rounded-full blur-3xl
                      transition-opacity duration-500
                      ${
                        isPrimary
                          ? "bg-primary/[0.08] group-hover:bg-primary/[0.12]"
                          : "bg-accent/[0.12] group-hover:bg-accent/[0.18]"
                      }
                    `}
                  />

                  {/* خط جانبي يعطي رسمية */}
                  <span
                    className={`
                      absolute inset-y-8 end-0 w-[3px] rounded-s-full
                      transition-all duration-300
                      group-hover:inset-y-6
                      ${
                        isPrimary
                          ? "bg-primary/70"
                          : "bg-accent"
                      }
                    `}
                  />

                  <div className="flex h-full flex-col">
                    {/* الصف العلوي */}
                    <div className="flex items-start justify-between gap-4">
                      <div
                        className={`
                          grid size-12 place-items-center rounded-2xl
                          border transition-all duration-300
                          group-hover:scale-105
                          ${
                            isPrimary
                              ? "border-primary/10 bg-primary/[0.07] text-primary"
                              : "border-accent/20 bg-accent/[0.12] text-accent-foreground"
                          }
                        `}
                      >
                        <Icon className="size-5.5" strokeWidth={1.6} />
                      </div>

                      <span className="font-mono text-xs font-semibold tracking-widest text-muted-foreground/45">
                        {number}
                      </span>
                    </div>

                    {/* المحتوى */}
                    <div className="mt-8">
                      <h2 className="font-display text-2xl font-bold tracking-tight text-foreground sm:text-[1.75rem]">
                        {title}
                      </h2>

                      <p className="mt-3 max-w-md text-sm leading-7 text-muted-foreground sm:text-[15px]">
                        {description}
                      </p>
                    </div>

                    {/* الإجراء */}
                    <div className="mt-auto pt-8">
                      <span
                        className={`
                          inline-flex items-center gap-2 text-sm font-semibold
                          ${
                            isPrimary
                              ? "text-primary"
                              : "text-accent-foreground"
                          }
                        `}
                      >
                        {cta}

                        <Arrow
                          className={`
                            size-4 transition-transform duration-300
                            ${
                              isRtl
                                ? "group-hover:-translate-x-1"
                                : "group-hover:translate-x-1"
                            }
                          `}
                        />
                      </span>
                    </div>
                  </div>
                </Link>
              );
            }
          )}
        </div>

        {/* ملاحظة ثقة */}
        <div className="mt-7 flex items-center justify-center gap-2 text-center text-xs text-muted-foreground/75 sm:text-sm">
          <ShieldCheck className="size-4 text-primary/70" />
          <span>
            {isRtl
              ? "سنرشدك خطوة بخطوة لإكمال البلاغ."
              : "We’ll guide you step by step to complete your report."}
          </span>
        </div>
      </div>
    </section>
  );
}