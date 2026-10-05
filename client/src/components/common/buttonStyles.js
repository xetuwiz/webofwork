/**
 * Button class recipes.
 *
 * Split out from `Button.jsx` because of `react-refresh/only-export-components`:
 * a module that exports both components and plain values breaks fast refresh
 * for every component in it, so the dev server falls back to a full reload on
 * each edit. This is the same split as `utils/toast.jsx` (helpers) and
 * `components/common/AppToaster.jsx` (component only).
 *
 * Exported so a `<Link>` or a `<div role="button">` that needs to look like a
 * Button can reuse the recipe without re-deriving it.
 */

export const BASE = [
  "inline-flex items-center justify-center gap-2",
  "rounded-lg font-bold whitespace-nowrap",
  "transition-colors duration-instant",
  "focus-visible:ring-2 focus-visible:ring-brand/40 focus-visible:outline-none",
  // Deliberately NOT `disabled:pointer-events-none`.
  //
  // `index.css` sets `cursor: not-allowed` on `button:disabled` so a greyed-out
  // control explains itself rather than just going quiet. `pointer-events-none`
  // would suppress the element's own cursor entirely and hand back the page
  // arrow, which reads as a rendering glitch instead of "temporarily
  // unavailable". The click is already blocked by the `disabled` attribute.
  "disabled:opacity-50",
].join(" ");

export const VARIANTS = {
  primary: "bg-brand text-brand-fg hover:bg-brand-hover",
  secondary: "bg-surface-2 text-fg border border-line hover:bg-surface-3",
  outline: "border border-line-strong text-fg hover:bg-surface-2",
  ghost: "text-fg-muted hover:bg-surface-3 hover:text-fg",
  danger: "bg-danger text-danger-fg hover:opacity-90",
  dangerGhost: "text-danger hover:bg-danger-soft",
};

export const SIZES = {
  sm: "h-9 px-3 text-xs",
  md: "h-11 px-4 text-sm",
  lg: "h-12 px-6 text-sm",
  full: "w-full h-12 px-4 text-sm",
  /**
   * No height or padding of its own.
   *
   * For the padding-driven buttons the migration found -- `py-3` +
   * `flex-1` + `rounded-2xl` and similar. Every size above sets a fixed `h-*`,
   * which would silently resize fourteen buttons that nobody asked to resize.
   * With `none`, the caller's own padding governs and only the *loading
   * behaviour* changes, which is the point of the migration.
   */
  none: "",
};

export const ICON_BOXES = {
  sm: "w-8 h-8",
  md: "w-10 h-10",
  lg: "w-11 h-11",
};
