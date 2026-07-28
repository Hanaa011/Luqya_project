const WAVE_COLOR = {
  default: "#0D7A6F",
  night: "#E7F3F1",
  onTeal: "#FFFFFF",
  mono: "currentColor",
};

const DOT_COLOR = {
  default: "#F0703A",
  night: "#F0703A",
  onTeal: "#F0703A",
  mono: "currentColor",
};

export default function Logo({
  tone = "default",
  mark = true,
  wordmark = false,
  glow = false,
  className = "size-8",
  wordmarkClassName = "text-2xl",
}) {
  const wave = WAVE_COLOR[tone] ?? WAVE_COLOR.default;
  const dot = DOT_COLOR[tone] ?? DOT_COLOR.default;

  const wordmarkToneClass = {
    default: "text-foreground",
    night: "text-[#F0F7F5]",
    onTeal: "text-white",
    mono: "text-current",
  }[tone];

  return (
    <span className="inline-flex items-center gap-2.5">
      {mark && (
        <span className={`relative inline-block shrink-0 ${className}`}>
          {glow && (
            <span
              className="absolute rounded-full animate-lantern-glow"
              style={{
                background: dot,
                opacity: 0.55,
                inset: "38% 8% 8% 38%",
              }}
              aria-hidden="true"
            />
          )}

          <svg
            viewBox="0 0 100 100"
            className="relative size-full"
            aria-hidden="true"
          >
            <path
              d="M61.95 58.37 A34 34 0 0 1 41.63 38.05"
              fill="none"
              stroke={wave}
              strokeWidth="7"
              strokeLinecap="round"
              opacity={tone === "mono" ? 0.45 : 0.5}
            />

            <path
              d="M50.67 62.48 A22 22 0 0 1 37.52 49.33"
              fill="none"
              stroke={wave}
              strokeWidth="8"
              strokeLinecap="round"
            />

            <circle cx="30" cy="70" r="8.5" fill={dot} />
          </svg>
        </span>
      )}

      {wordmark && (
        <span
          className={`font-display font-extrabold tracking-tight ${wordmarkToneClass} ${wordmarkClassName}`}
        >
          لُقيا
        </span>
      )}
    </span>
  );
}