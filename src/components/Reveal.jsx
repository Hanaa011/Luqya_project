import { Children, isValidElement } from "react";
import { useInView } from "../hooks/useInView";

// Physical, not logical, offsets on purpose: every current use of
// direction="left"/"right" in this codebase animates content whose visual
// position doesn't reorder between Arabic/English (see Home.jsx), so a
// fixed screen-space offset reads correctly in both directions without
// needing to flip based on document direction.
const OFFSETS = {
  up: "translateY(22px)",
  down: "translateY(-22px)",
  left: "translateX(-24px)",
  right: "translateX(24px)",
  scale: "scale(0.97)",
  none: "none",
};

/**
 * Wraps its children in a viewport-triggered reveal animation.
 *
 * <Reveal direction="up" delay={80}>
 *   <Card />
 * </Reveal>
 *
 * By default animates once, the first time it scrolls into view, and
 * stays revealed after that (does not replay on scroll-up re-entry) —
 * pass `once={false}` to opt into replay-on-every-entry instead. Fully
 * inert when the user has requested reduced motion (see the .reveal /
 * prefers-reduced-motion rule in index.css, which forces full opacity
 * and no transform regardless of inView state).
 */
export default function Reveal({
  children,
  as: Tag = "div",
  direction = "up",
  delay = 0,
  duration = 600,
  threshold = 0.15,
  rootMargin = "-10% 0px -10% 0px",
  once = true,
  className = "",
  style = {},
  ...rest
}) {
  const [ref, inView] = useInView({ threshold, rootMargin, once });
  const offset = OFFSETS[direction] ?? OFFSETS.up;

  return (
    <Tag
      ref={ref}
      className={`reveal ${className}`}
      style={{
        ...style,
        transitionDuration: `${duration}ms`,
        transitionDelay: inView ? `${delay}ms` : "0ms",
        opacity: inView ? 1 : 0,
        transform: inView ? "translate(0, 0) scale(1)" : offset,
      }}
      {...rest}
    >
      {children}
    </Tag>
  );
}

/**
 * Applies a Reveal to each direct child with an incrementing stagger,
 * without needing a separate Reveal wrapper pasted around every item.
 *
 * <RevealGroup direction="up" stagger={90}>
 *   {items.map((item) => <Card key={item.id} {...item} />)}
 * </RevealGroup>
 *
 * Each child keeps its own key; Reveal's wrapper element is a plain
 * `div` by default, which is a valid, non-disruptive grid/flex item, so
 * this is safe to use directly inside CSS Grid/Flex layouts.
 */
export function RevealGroup({ children, stagger = 80, baseDelay = 0, ...revealProps }) {
  return Children.map(children, (child, index) => {
    if (!isValidElement(child)) return child;
    return (
      <Reveal key={child.key ?? index} delay={baseDelay + index * stagger} {...revealProps}>
        {child}
      </Reveal>
    );
  });
}
