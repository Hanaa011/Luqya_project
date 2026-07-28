/**
 * DammaMark — the small curl of the ضمة above the قin لُقيا, lifted out of
 * the wordmark and reused as the platform's recurring brand gesture: the
 * loading indicator, section bullets, empty-state glyphs, and OTP accents
 * all trace back to this one shape instead of borrowing generic AI icons.
 */
export default function DammaMark({
  className = "size-5",
  spin = false,
  glow = false,
}) {
  return (
    <span className={`relative inline-flex ${className}`}>
      {glow && (
        <svg
          viewBox="0 0 24 24"
          fill="none"
          className="absolute inset-0 size-full blur-[6px] opacity-70 text-primary"
          aria-hidden="true"
        >
          <path
            d="M7 13.5C7 9.6 10.1 6.5 14 6.5C16.8 6.5 19 8.5 19 11C19 13 17.4 14.5 15.4 14.5C13.9 14.5 12.7 13.5 12.7 12.1"
            stroke="currentColor"
            strokeWidth="2.4"
            strokeLinecap="round"
          />
        </svg>
      )}

      <svg
        viewBox="0 0 24 24"
        fill="none"
        className={`relative size-full ${spin ? "animate-damma-spin" : ""}`}
        aria-hidden="true"
      >
        <path
          d="M7 13.5C7 9.6 10.1 6.5 14 6.5C16.8 6.5 19 8.5 19 11C19 13 17.4 14.5 15.4 14.5C13.9 14.5 12.7 13.5 12.7 12.1"
          stroke="currentColor"
          strokeWidth="2.4"
          strokeLinecap="round"
        />
      </svg>
    </span>
  );
}
