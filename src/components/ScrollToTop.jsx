import { useEffect, useRef } from "react";
import { useLocation } from "react-router-dom";

// Standard easeInOutCubic — a calm, symmetrical acceleration/deceleration
// curve, the same general shape used by Apple/Linear/Vercel-style page
// transitions: gentle start, faster middle, gentle settle. Native
// `window.scrollTo({ behavior: "smooth" })` can't be used to hit a
// specific duration or easing curve — every browser picks its own fixed
// timing for it — so this animates scrollY manually via
// requestAnimationFrame to get precise, consistent control instead.
function easeInOutCubic(t) {
  return t < 0.5 ? 4 * t * t * t : 1 - Math.pow(-2 * t + 2, 3) / 2;
}

const DURATION_MS = 700;

/**
 * React Router's client-side navigation never resets scroll position on
 * its own. Mounted once near the top of the app (see App.jsx), this
 * watches the route and smoothly animates scroll back to the top on
 * every pathname change, everywhere in the app — Footer, Navbar, Hero
 * CTAs, Dashboard links, all of it, with no per-link logic anywhere.
 */
export default function ScrollToTop() {
  const { pathname } = useLocation();
  const frameRef = useRef(null);

  useEffect(() => {
    // Respect the person's OS-level motion preference — an accessibility
    // baseline, not optional polish. Jump instantly instead of animating.
    const prefersReducedMotion = window.matchMedia("(prefers-reduced-motion: reduce)").matches;

    const startY = window.scrollY;
    if (startY === 0) return undefined;

    if (prefersReducedMotion) {
      window.scrollTo(0, 0);
      return undefined;
    }

    // If a previous transition's animation is still running (e.g. two
    // navigations happen in quick succession), cancel it cleanly rather
    // than letting two competing scroll animations fight each other.
    if (frameRef.current) {
      cancelAnimationFrame(frameRef.current);
      frameRef.current = null;
    }

    let startTime = null;

    function step(timestamp) {
      if (startTime === null) startTime = timestamp;
      const elapsed = timestamp - startTime;
      const progress = Math.min(elapsed / DURATION_MS, 1);
      const eased = easeInOutCubic(progress);

      window.scrollTo(0, Math.round(startY * (1 - eased)));

      if (progress < 1) {
        frameRef.current = requestAnimationFrame(step);
      } else {
        frameRef.current = null;
      }
    }

    frameRef.current = requestAnimationFrame(step);

    return () => {
      if (frameRef.current) {
        cancelAnimationFrame(frameRef.current);
        frameRef.current = null;
      }
    };
  }, [pathname]);

  return null;
}
