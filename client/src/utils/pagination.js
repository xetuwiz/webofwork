/**
 * Pagination maths.
 *
 * Split out of `Pagination.jsx` for the same `react-refresh/only-export-components`
 * reason as `buttonStyles.js` / `formStyles.js`: a module that exports both
 * components and plain values breaks fast refresh for every component in it.
 */

/**
 * 20 must be in this list: it is the default page size, so a select without it
 * has no matching option and the browser falls back to rendering the first
 * entry -- showing "10" while the query is asking for 20.
 */
export const PAGE_SIZE_OPTIONS = [10, 20, 25, 50, 100];

/** The default, kept next to the options so the two cannot drift apart. */
export const DEFAULT_PAGE_SIZE = 20;

/**
 * Page numbers with gaps, e.g. `[1, 'gap', 7, 8, 9, 'gap', 24]`.
 *
 * The button count stays constant as the current page moves, which is the point:
 * rendering `1..totalPages` makes the footer change width on every page, so the
 * whole row jumps around while you are trying to read it.
 *
 * @param {number} current 1-based
 * @param {number} total
 * @param {number} [siblings] how many pages either side of `current` to keep
 */
export function pageWindow(current, total, siblings = 1) {
  if (total <= 0) return [];
  const pages = new Set([1, total]);
  for (let p = current - siblings; p <= current + siblings; p++) {
    if (p >= 1 && p <= total) pages.add(p);
  }
  // If every page would fit in the window anyway, show them all rather than
  // a window with no gaps in it.
  if (pages.size >= total) return Array.from({ length: total }, (_, i) => i + 1);

  const sorted = [...pages].sort((a, b) => a - b);
  const out = [];
  let prev = 0;
  for (const p of sorted) {
    if (prev && p - prev > 1) out.push("gap");
    out.push(p);
    prev = p;
  }
  return out;
}
