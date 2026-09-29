/**
 * Skeleton placeholder.
 *
 * ## Why shimmer rather than `animate-pulse`
 *
 * The app had 15 `animate-pulse` uses, but they are two different things:
 * roughly eight are grey placeholder rectangles, and the rest are status dots
 * and "Loading..." text. A flat pulse on a placeholder reads as *disabled* -- it
 * fades in place, so the eye has nothing to track. A shimmer sweeps a highlight
 * across it, which reads as motion and therefore as work in progress.
 *
 * The status dots and loading text keep `animate-pulse`, which is correct for
 * them: a sweep across a 2px dot or a line of text is noise.
 *
 * ## Accessibility
 *
 * `aria-hidden` by default. A skeleton is decoration standing in for content
 * that is not there yet; announcing it would put "image" or "group" in the
 * accessibility tree for every placeholder. Pass `label` when a placeholder is
 * the *only* indicator of what is loading, and it becomes a `role="status"`
 * announcement instead.
 *
 * The shimmer runs continuously, and the global `prefers-reduced-motion` block
 * floors `animation-duration` to 0.01ms, so it stops sweeping rather than
 * looping forever.
 */
export default function Skeleton({ className = "", label }) {
  if (label) {
    return (
      <div role="status" aria-live="polite" className={`shimmer ${className}`}>
        <span className="sr-only">{label}</span>
      </div>
    );
  }
  return <div aria-hidden="true" className={`shimmer ${className}`} />;
}
