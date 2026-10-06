/**
 * URL-backed list state: page, page size, filters and sort.
 *
 * ## Why
 *
 * 21 list pages held page number, search term, status filter and sort in React
 * state only. That produced three concrete failures:
 *
 *  * **Not shareable.** There was no URL for "contracts, page 3, status
 *    disputed", so a support answer could only be "go to Contracts and click
 *    Next twice".
 *  * **Back button was wrong.** After filtering, Back returned to the previous
 *    *page* rather than the previous filter, because the filter change never
 *    entered history.
 *  * **Refresh lost position**, and each page re-implemented the same filter
 *    plus page wiring -- which is why they drifted apart.
 *
 * React Router's `useSearchParams` already does the hard part, so this is a
 * thin typed layer over it rather than a state manager.
 *
 * ## Page resets on filter change
 *
 * Changing a filter resets to page 1. Without that you land on page 7 of a
 * result set that now has 2 pages and see an empty table, which reads as "the
 * filter is broken" rather than "you are past the end".
 *
 * ## The one deliberate non-sync: the search box
 *
 * Search is debounced before it reaches the URL (see `useDebouncedValue`).
 * Writing every keystroke to the address bar would put an entry in history per
 * character and make Back useless. Wire the debounced value in via
 * `setFilter("search", debounced)`.
 */
import { useCallback, useMemo } from "react";
import { useSearchParams } from "react-router-dom";
import { DEFAULT_PAGE_SIZE } from "../utils/pagination";

// Re-exported so list pages can import them alongside the hook. Defined in
// utils/pagination alongside the windowing maths they belong with. The default
// is re-read from there rather than hard-coded, so the fallback and the
// page-size select can never disagree.
export { PAGE_SIZE_OPTIONS, DEFAULT_PAGE_SIZE } from "../utils/pagination";

/**
 * @param {object} [options]
 * @param {Record<string, string>} [options.defaults] Initial value per key.
 *   Anything listed here is treated as a filter for reset/page-reset purposes.
 * @param {string[]} [options.persist] Keys kept in the URL. Defaults to every
 *   key in `defaults`; pass an explicit list to leave the search term out.
 */
export function useListState({ defaults = {}, persist, prefix = "" } = {}) {
  const [params, setParams] = useSearchParams();

  const keys = useMemo(
    () => persist ?? Object.keys(defaults),
    [persist, defaults],
  );

  // CategoriesAndSkills renders two independent lists (categories, skills) on one
  // page. Without a prefix they would both read and write `page`, so paging one
  // would silently reset the other. `prefix: "cat_"` gives each list its own
  // `cat_page` / `cat_page_size` / `cat_search`.
  const name = useCallback((key) => `${prefix}${key}`, [prefix]);

  const get = useCallback(
    (key) => params.get(name(key)) ?? defaults[key] ?? "",
    [params, defaults, name],
  );

  const page = Math.max(Number(params.get(name("page"))) || 1, 1);
  const pageSizeRaw = Number(params.get(name("page_size")));
  const pageSize = pageSizeRaw > 0 ? pageSizeRaw : DEFAULT_PAGE_SIZE;

  /** Replace one key. `resetPage` is the default because filters narrow results. */
  const set = useCallback(
    (key, value, { resetPage = true } = {}) => {
      setParams(
        (prev) => {
          const next = new URLSearchParams(prev);
          const k = name(key);
          if (value === "" || value === null || value === undefined) {
            next.delete(k);
          } else {
            next.set(k, String(value));
          }
          if (resetPage) next.delete(name("page"));
          return next;
        },
        { replace: true },
      );
    },
    [setParams, name],
  );

  /**
   * Replace several keys in one navigation.
   *
   * ## Why this exists
   *
   * `set` looks atomic but is not. React Router applies each `setSearchParams`
   * call against the params **as of that render**, so two calls in one handler
   * do not compose -- the second is computed from a snapshot that does not
   * contain the first, and it wins:
   *
   *     set("categoryId", "2");   // writes categoryId=2
   *     set("minPrice", "10");    // writes minPrice=10, computed from the state
   *                              // *before* categoryId was set
   *     // URL: ?minPrice=10      -- categoryId is gone
   *
   * Nothing throws and no request fires with the lost value, so this is
   * invisible until you look at the URL. On Find Talent it meant pressing
   * "Apply Filters" filtered by nothing at all: `handleApplyFilters` sets three
   * keys, and the last one won, so a category selection was discarded.
   *
   * Six pages had this shape (FindTalent, FindJobs, ClientContracts, Contracts,
   * MyProjects). Fixing each call site would have left the next one to
   * reintroduce it, so the batched form is the fix and the call sites just use
   * it.
   *
   * ## Why not fix `set` itself
   *
   * A ref-accumulator inside `set` would fix it without touching call sites, but
   * it would also make `set` depend on render timing -- the accumulated value
   * would have to be flushed somehow, and a component that unmounts mid-handler
   * would drop it. An explicit batch has no hidden state and no flush to get
   * wrong.
   *
   * @param {Record<string, string|null|undefined>} values Keys to replace.
   * @param {{ resetPage?: boolean }} [options]
   */
  const setMany = useCallback(
    (values, { resetPage = true } = {}) => {
      setParams(
        (prev) => {
          const next = new URLSearchParams(prev);
          for (const [key, value] of Object.entries(values)) {
            const k = name(key);
            if (value === "" || value === null || value === undefined) {
              next.delete(k);
            } else {
              next.set(k, String(value));
            }
          }
          if (resetPage) next.delete(name("page"));
          return next;
        },
        { replace: true },
      );
    },
    [setParams, name],
  );

  const setPage = useCallback(
    (next) => {
      setParams(
        (prev) => {
          const q = new URLSearchParams(prev);
          const key = name("page");
          const p = Math.max(Number(next) || 1, 1);
          if (p <= 1) q.delete(key);
          else q.set(key, String(p));
          return q;
        },
        // Paging is a navigation, so it gets a history entry -- unlike changing
        // a filter, which should not.
        { replace: false },
      );
    },
    [setParams, name],
  );

  const setPageSize = useCallback(
    (next) => {
      setParams(
        (prev) => {
          const q = new URLSearchParams(prev);
          const key = name("page_size");
          const size = Math.max(Number(next) || DEFAULT_PAGE_SIZE, 1);
          // The default size is left out of the URL to keep ordinary links short.
          if (size === DEFAULT_PAGE_SIZE) q.delete(key);
          else q.set(key, String(size));
          // A different page size can move the current page out of range.
          q.delete(name("page"));
          return q;
        },
        { replace: true },
      );
    },
    [setParams, name],
  );

  const resetFilters = useCallback(() => {
    setParams(
      (prev) => {
        const next = new URLSearchParams(prev);
        for (const key of keys) next.delete(name(key));
        next.delete(name("page"));
        return next;
      },
      { replace: true },
    );
  }, [keys, setParams, name]);

  const filters = useMemo(
    () => Object.fromEntries(keys.map((k) => [k, get(k)])),
    // `get` already depends on `params`.
    // eslint-disable-next-line react-hooks/exhaustive-deps
    [keys, params, name],
  );

  // A filter counts as active when it *differs from its default*, not when it is
  // merely non-empty. Several pages use a meaningful default that lives in the
  // URL: Contracts.jsx defaults `tab` to "All", which means "no filter". Testing
  // against "" marked that as an active filter, so a freelancer with nothing
  // applied was told "No contracts match your filters".
  const hasActiveFilters = useMemo(
    () => keys.some((k) => (params.get(name(k)) ?? defaults[k] ?? "") !== (defaults[k] ?? "")),
    [keys, params, defaults, name],
  );

  return {
    page,
    pageSize,
    filters,
    get,
    set,
    setMany,
    setPage,
    setPageSize,
    resetFilters,
    hasActiveFilters,
    persistKey: keys,
  };
}

/**
 * The query object to hand a react-query hook, with empty values dropped.
 *
 * Sending `search=` and `status=` as empty strings works, but it produces
 * cache keys that differ for "unset" and "set to empty" -- two entries for the
 * same result set. Stripping them keeps the cache honest.
 */
export function toQueryParams(filters, page, pageSize, extra = {}) {
  const out = {};
  for (const [k, v] of Object.entries({ ...filters, ...extra })) {
    if (v !== "" && v !== null && v !== undefined) out[k] = v;
  }
  return { ...out, page, page_size: pageSize };
}
