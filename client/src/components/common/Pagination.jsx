/**
 * Pagination footer: range counter, page-size selector, numbered pages.
 *
 * ## Why
 *
 * 19 list pages gated their arrows on `data.next` / `data.previous` and read
 * `data.count` in some places -- but none of them showed either the total or
 * the current position. So a user on page 4 of an unknown number of pages had
 * no way to know how much was left except clicking Next until the arrow
 * stopped responding.
 *
 * The count is already on the wire. This just reads it.
 *
 * ## Why numbered pages and not just arrows
 *
 * Arrows alone force a user who knows they want page 9 to click eight times,
 * and make "go to the last page" impossible. That is the common case for an
 * admin triaging a long dispute list.
 *
 * ## The windowing
 *
 * Always shows first, last, and a window around the current page, with gaps
 * rendered as an inert separator. Keeping the button count constant means the
 * footer does not reflow its own width as you page, which is what makes a naive
 * `1..totalPages` implementation jump around.
 */
import { ChevronLeft, ChevronRight } from "lucide-react";
import { PAGE_SIZE_OPTIONS, pageWindow } from "../../utils/pagination";

const BTN =
  "h-9 min-w-9 px-2 rounded-lg text-sm font-semibold transition-colors " +
  "focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-brand/40";
const IDLE = "text-fg-muted hover:bg-surface-3 hover:text-fg";
const ACTIVE = "bg-brand text-brand-fg";
const OFF = "text-fg-subtle opacity-40 cursor-not-allowed";

export default function Pagination({
  page,
  pageSize,
  count,
  onPageChange,
  onPageSizeChange,
  itemNoun = "results",
  showPageSize = true,
  className = "",
}) {
  const safeCount = Math.max(Number(count) || 0, 0);
  const totalPages = Math.max(Math.ceil(safeCount / Math.max(pageSize, 1)), 1);
  const current = Math.min(Math.max(page, 1), totalPages);

  if (safeCount === 0) return null;

  // "Showing 21-40 of 842". Computed from the *clamped* page so the range
  // cannot read "Showing 81-100 of 40" after a page-size change.
  const from = (current - 1) * pageSize + 1;
  const to = Math.min(current * pageSize, safeCount);
  const window = pageWindow(current, totalPages);

  return (
    <div
      className={`flex flex-col sm:flex-row items-center justify-between gap-3 pt-4 ${className}`}
    >
      <div className="flex items-center gap-4 flex-wrap justify-center">
        <p className="text-xs text-fg-subtle" aria-live="polite">
          Showing <span className="font-semibold text-fg-muted">{from}</span>–
          <span className="font-semibold text-fg-muted">{to}</span> of{" "}
          <span className="font-semibold text-fg-muted">{safeCount}</span>{" "}
          {itemNoun}
        </p>

        {showPageSize ? (
          <label className="flex items-center gap-2 text-xs text-fg-subtle">
            <span className="hidden sm:inline">Per page</span>
            <select
              value={pageSize}
              onChange={(e) => onPageSizeChange(Number(e.target.value))}
              aria-label={`${itemNoun} per page`}
              className="h-9 rounded-lg border border-line bg-surface-2 px-2 text-xs font-semibold text-fg-muted focus-visible:outline-none focus-visible:border-brand"
            >
              {PAGE_SIZE_OPTIONS.map((n) => (
                <option key={n} value={n}>
                  {n}
                </option>
              ))}
            </select>
          </label>
        ) : null}
      </div>

      {totalPages > 1 ? (
        <nav aria-label="Pagination" className="flex items-center gap-1">
          <button
            type="button"
            onClick={() => onPageChange(current - 1)}
            disabled={current <= 1}
            aria-label="Previous page"
            className={`${BTN} ${current <= 1 ? OFF : IDLE}`}
          >
            <ChevronLeft className="w-4 h-4" aria-hidden="true" />
          </button>

          {window.map((p, i) =>
            p === "gap" ? (
              // Inert: the ellipsis is a visual gap, not a control, so it is
              // hidden from assistive tech rather than exposed as a dead button.
              <span
                key={`gap-${i}`}
                aria-hidden="true"
                className="px-1 text-fg-subtle select-none"
              >
                …
              </span>
            ) : (
              <button
                key={p}
                type="button"
                onClick={() => onPageChange(p)}
                aria-current={p === current ? "page" : undefined}
                aria-label={`Page ${p}`}
                className={`${BTN} ${p === current ? ACTIVE : IDLE}`}
              >
                {p}
              </button>
            ),
          )}

          <button
            type="button"
            onClick={() => onPageChange(current + 1)}
            disabled={current >= totalPages}
            aria-label="Next page"
            className={`${BTN} ${current >= totalPages ? OFF : IDLE}`}
          >
            <ChevronRight className="w-4 h-4" aria-hidden="true" />
          </button>
        </nav>
      ) : null}
    </div>
  );
}
