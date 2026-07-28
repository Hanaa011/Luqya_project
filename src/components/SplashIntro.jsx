import { useEffect, useState } from "react";
import { useI18n } from "../lib/useI18n";

const TIMINGS = {
  waves: 150, // wave arcs begin drawing in
  dot: 650, // the coral dot ignites
  word: 1150, // لُقيا reveals, with its damma — the guide's "first mention"
  tagline: 1750,
  exit: 2500, // the whole scene lifts and dissolves into the app
  done: 3050,
};

export default function SplashIntro({ onDone }) {
  const { locale } = useI18n();
  const [stage, setStage] = useState("enter");
  const reduced =
    typeof window !== "undefined" &&
    Boolean(window.matchMedia?.("(prefers-reduced-motion: reduce)").matches);

  useEffect(() => {
    if (reduced) {
      const id = window.setTimeout(onDone, 350);
      return () => window.clearTimeout(id);
    }

    const timers = [
      window.setTimeout(() => setStage("waves"), TIMINGS.waves),
      window.setTimeout(() => setStage("dot"), TIMINGS.dot),
      window.setTimeout(() => setStage("word"), TIMINGS.word),
      window.setTimeout(() => setStage("tagline"), TIMINGS.tagline),
      window.setTimeout(() => setStage("exit"), TIMINGS.exit),
      window.setTimeout(onDone, TIMINGS.done),
    ];

    return () => timers.forEach(window.clearTimeout);
  }, [reduced, onDone]);

  const revealed = ["waves", "dot", "word", "tagline", "exit"].includes(stage);
  const dotIn = ["dot", "word", "tagline", "exit"].includes(stage);
  const wordIn = ["word", "tagline", "exit"].includes(stage);
  const taglineIn = ["tagline", "exit"].includes(stage);
  const exiting = stage === "exit";

  return (
    <div
      className={`fixed inset-0 z-[999] grid place-items-center bg-[#0B1614] transition-all duration-700 ${
        exiting ? "opacity-0 scale-[1.04] pointer-events-none" : "opacity-100 scale-100"
      }`}
      style={{ transitionTimingFunction: "cubic-bezier(0.16,1,0.3,1)" }}
      role="presentation"
    >
      <div className="absolute inset-0 bg-aurora-ink" />

      <div className="relative flex flex-col items-center">
        <div className="relative size-28 sm:size-32">
          <span
            className={`absolute rounded-full bg-[#F0703A] blur-xl transition-all duration-700 ${
              dotIn ? "opacity-60 scale-100" : "opacity-0 scale-50"
            }`}
            style={{ inset: "40% 10% 10% 40%" }}
          />

          <svg viewBox="0 0 100 100" className="relative size-full">
            <path
              d="M61.95 58.37 A34 34 0 0 1 41.63 38.05"
              fill="none"
              stroke="#E7F3F1"
              strokeWidth="6"
              strokeLinecap="round"
              opacity="0.45"
              pathLength="1"
              style={{
                strokeDasharray: 1,
                strokeDashoffset: revealed ? 0 : 1,
                transition: "stroke-dashoffset 900ms cubic-bezier(0.16,1,0.3,1) 80ms",
              }}
            />
            <path
              d="M50.67 62.48 A22 22 0 0 1 37.52 49.33"
              fill="none"
              stroke="#12968A"
              strokeWidth="7"
              strokeLinecap="round"
              pathLength="1"
              style={{
                strokeDasharray: 1,
                strokeDashoffset: revealed ? 0 : 1,
                transition: "stroke-dashoffset 900ms cubic-bezier(0.16,1,0.3,1) 260ms",
              }}
            />
            <circle
              cx="30"
              cy="70"
              r="8.5"
              fill="#F0703A"
              style={{
                transformOrigin: "30px 70px",
                transform: dotIn ? "scale(1)" : "scale(0)",
                opacity: dotIn ? 1 : 0,
                transition: "transform 500ms cubic-bezier(0.34,1.56,0.64,1), opacity 300ms ease",
              }}
            />
          </svg>
        </div>

        <div
          className="font-display font-extrabold text-4xl sm:text-5xl text-[#F0F7F5] mt-6 transition-all duration-700"
          style={{
            opacity: wordIn ? 1 : 0,
            transform: wordIn ? "translateY(0)" : "translateY(10px)",
            filter: wordIn ? "blur(0px)" : "blur(6px)",
          }}
        >
          لُقيا
        </div>

        <p
          className="mt-3 text-sm text-[#9AA6A3] transition-all duration-700"
          style={{
            opacity: taglineIn ? 1 : 0,
            transform: taglineIn ? "translateY(0)" : "translateY(6px)",
          }}
        >
          {locale === "ar"
            ? "الذكاء الاصطناعي يبحث..."
            : locale === "ur"
            ? "اے آئی تلاش کر رہا ہے..."
            : "AI is searching..."}
        </p>
      </div>

      <button
        type="button"
        onClick={onDone}
        className="absolute bottom-8 text-xs font-medium text-[#61706C] hover:text-[#E7F3F1] transition-colors tracking-wide"
      >
        {locale === "ar" ? "تخطّي" : locale === "ur" ? "نظرانداز کریں" : "Skip"}
      </button>
    </div>
  );
}
