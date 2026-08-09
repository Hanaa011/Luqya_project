import { useEffect, useRef, useState } from "react";

/**
 * Tracks whether an element is currently intersecting the viewport.
 *
 * By default (`once: false`) there is no "hasAnimated" latch — `inView`
 * toggles back to false the moment the element leaves, so a consumer
 * naturally replays on every re-entry, scrolling down or back up.
 *
 * Pass `once: true` to latch permanently on first entry instead: once
 * `inView` becomes true, the observer disconnects and the value never
 * reverts to false again, so the animation plays exactly once, only
 * while scrolling down past it for the first time.
 */
export function useInView({ threshold = 0.15, rootMargin = "-10% 0px -10% 0px", once = false } = {}) {
  const ref = useRef(null);
  const [inView, setInView] = useState(false);

  useEffect(() => {
    const node = ref.current;
    if (!node) return undefined;

    const observer = new IntersectionObserver(
      ([entry]) => {
        if (entry.isIntersecting) {
          setInView(true);
          if (once) observer.disconnect();
        } else if (!once) {
          setInView(false);
        }
      },
      { threshold, rootMargin }
    );

    observer.observe(node);
    return () => observer.disconnect();
  }, [threshold, rootMargin, once]);

  return [ref, inView];
}
