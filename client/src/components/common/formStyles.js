/**
 * Shared form-control class recipes.
 *
 * Split from `Field.jsx` for the same `react-refresh/only-export-components`
 * reason as `buttonStyles.js`. Exported so inputs that are not wrapped in a
 * `Field` -- a search box in a sticky header, a filter input in a table header
 * -- can still match the rest of the forms.
 */
export const CONTROL = [
  "w-full rounded-lg border bg-surface-2 px-3 text-sm text-fg",
  "placeholder:text-fg-subtle",
  "transition-colors duration-instant",
  "focus-visible:outline-none focus-visible:border-brand focus-visible:ring-2 focus-visible:ring-brand/20",
  "disabled:opacity-50 disabled:cursor-not-allowed",
].join(" ");
