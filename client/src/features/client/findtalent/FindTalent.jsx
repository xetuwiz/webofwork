import React, { useEffect, useState } from "react";
import { Search, SlidersHorizontal, ArrowLeft, X, Filter, Loader2 } from "lucide-react";
import Pagination from "../../../components/common/Pagination";
import EmptyState from "../../../components/common/EmptyState";
import { useListState } from "../../../hooks/useListState";
import { Link } from "react-router-dom";
import { FreelancerCard } from "./components/FreelancerCard";
import { useFreelancerList } from "./findTalentQueries";
import { useAllCategories } from "../projectManagement/projectQueries";
import { useDebouncedValue } from "../../../hooks/useDebounce";
import { toastError } from "../../../utils/toast";

export default function FindTalent() {
  const [showMobileFilters, setShowMobileFilters] = useState(false);
  const [searchValue, setSearchValue] = useState("");

  // The filters were three raw DOM refs read out of the form at submit time and
  // cleared by writing `ref.current.value = ""` in the reset handler. That is a
  // React anti-pattern with two real consequences: the form's state is invisible
  // to React, so it cannot reset itself when the query changes, and the
  // values only exist once the user has typed something -- there was no way to
  // show or validate them ahead of submit. Controlled state fixes both.
  const [categoryId, setCategoryId] = useState("");
  const [minPrice, setMinPrice] = useState("");
  const [maxPrice, setMaxPrice] = useState("");

  // Only the *applied* filters, the search term and paging live in the URL. The
  // draft values below stay local on purpose: a half-typed budget is not a filter
  // the user has asked for yet, and putting it in the URL would fire a request
  // per keystroke and make a shared link carry half-finished input.
  const {
    page,
    pageSize,
    filters: appliedFilters,
    set: setFilter,
    setPage,
    setPageSize,
    hasActiveFilters,
    resetFilters: resetAllFilters,
  } = useListState({
    defaults: { search: "", categoryId: "", minPrice: "", maxPrice: "" },
  });

  /**
   * Validate the budget range.
   *
   * This used two `alert()` calls, which block the main thread, ignore the
   * theme, and cannot be styled or translated. Replaced with a toast and an
   * inline message, so the reason the filter did not apply is visible where the
   * filter is.
   */
  const budgetError = (() => {
    const min = minPrice === "" ? null : Number(minPrice);
    const max = maxPrice === "" ? null : Number(maxPrice);
    if (min !== null && Number.isNaN(min)) return "Minimum budget must be a number.";
    if (max !== null && Number.isNaN(max)) return "Maximum budget must be a number.";
    if (min !== null && min < 0) return "Minimum budget can't be negative.";
    if (max !== null && max < 0) return "Maximum budget can't be negative.";
    if (min !== null && max !== null && max < min) {
      return "Maximum budget can't be lower than the minimum.";
    }
    return null;
  })();

  /**
   * Typing searches on its own after a short pause; Enter and Apply apply
   * immediately.
   *
   * Previously nothing happened until Enter or "Apply Filters", which is the
   * most common way people expect a search box to behave and is the reason the
   * page read as "search is not working".
   */
  const [debouncedSearch, isSearchPending] = useDebouncedValue(searchValue, 400);

  // Derived, not synchronised: every value below is computed from state that is
  // already declared above it.
  //
  // An earlier version cached the last valid query in a `useRef` and read/wrote
  // `ref.current` during render to hold the previous results while the budget
  // range was invalid. That was wrong twice over. It read a ref during render,
  // which React's `react-hooks/refs` rule rejects and which is genuinely unsafe
  // under concurrent rendering -- a render can be started and thrown away, so a
  // value written during it is not reliable. And a frozen snapshot can disagree
  // with what the boxes on screen actually show.
  //
  // Omitting the offending filter is both safe and more honest: the list keeps
  // matching the search term and category the user can see, and the inline
  // `budgetError` message above the fields explains why the budget is not being
  // applied. This matters because `ProfilesController.List` binds minPrice and
  // maxPrice as `decimal?` -- an unbindable value is a model-binding failure,
  // which ASP.NET turns into a 400 for the whole request, not a dropped filter.
  const queryParams = {
    search: appliedFilters.search || undefined,
    categoryId: appliedFilters.categoryId || undefined,
    minPrice: budgetError ? undefined : appliedFilters.minPrice || undefined,
    maxPrice: budgetError ? undefined : appliedFilters.maxPrice || undefined,
    page,
    page_size: pageSize,
  };

  // Declared after `queryParams` on purpose. This call reads the const above, so
  // the two cannot be swapped: doing so was a temporal-dead-zone crash that
  // threw "can't access lexical declaration 'queryParams' before initialization"
  // on the first render and left the page blank. `npm run build` stayed green
  // through all of it, because Rollup does not do TDZ analysis -- the reference
  // is valid at module scope and only fails when the function actually runs.
  const { data, isLoading, isFetching } = useFreelancerList(queryParams);
  const { data: categories } = useAllCategories();

  // Push the debounced term into the URL so a search is shareable. Debounced
  // first, then written once, rather than on every keystroke -- otherwise Back
  // would step back one character at a time. The field itself stays local, so
  // typing is never interrupted by the URL updating mid-word.
  useEffect(() => {
    const term = debouncedSearch.trim();
    if (term !== (appliedFilters.search ?? "")) {
      setFilter("search", term);
    }
  }, [debouncedSearch, appliedFilters.search, setFilter]);

  // Re-seed the field from the URL so a shared link shows its term. Render-time
  // adjustment rather than an effect, which React documents for this case.
  const [lastSynced, setLastSynced] = useState(appliedFilters.search);
  if (appliedFilters.search !== lastSynced) {
    setLastSynced(appliedFilters.search);
    setSearchValue(appliedFilters.search);
  }

  const handleSearchChange = (value) => {
    setSearchValue(value);
  };

  const handleApplyFilters = () => {
    if (budgetError) {
      toastError(budgetError);
      return;
    }
    // Each write drops `page`, so the three of them together are also
    // "return to page 1".
    setFilter("categoryId", categoryId);
    setFilter("minPrice", minPrice);
    setFilter("maxPrice", maxPrice);
    setShowMobileFilters(false);
  };

  const handleKeyDown = (e) => {
    if (e.key === "Enter") handleApplyFilters();
  };

  const clearSearch = () => {
    setSearchValue("");
    setFilter("search", "");
  };

  const resetFilters = () => {
    setSearchValue("");
    setCategoryId("");
    setMinPrice("");
    setMaxPrice("");
    // One write clears every filter key and the page, instead of six setters
    // that could leave the page on 3 of a now-single-page result.
    resetAllFilters();
    setShowMobileFilters(false);
  };

  return (
    <div className="min-h-screen bg-surface-2">
      <div className="bg-surface border-b border-line pt-10 pb-8 md:pt-16 md:pb-12">
        <div className="max-w-7xl mx-auto px-4">
          <div className="text-center max-w-3xl mx-auto">
            <h1 className="text-3xl md:text-4xl font-extrabold text-fg tracking-tight">
              Find & Hire Top <span className="text-brand">Talent</span>
            </h1>
            <div className="mt-6 md:mt-8 relative group">
              <div className="absolute inset-y-0 left-0 pl-4 md:pl-5 flex items-center pointer-events-none">
                <Search className="h-5 w-5 text-fg-subtle group-focus-within:text-brand transition-colors" />
              </div>
              <input
                type="text"
                value={searchValue}
                onChange={(e) => handleSearchChange(e.target.value)}
                onKeyDown={handleKeyDown}
                placeholder="Search skill, name..."
                aria-label="Search freelancers by skill or name"
                className="block w-full pl-11 pr-11 py-3.5 md:py-4.5 bg-surface border border-line rounded-2xl shadow-sm focus-visible:ring-4 focus-visible:ring-brand/20 focus-visible:border-brand outline-none transition-all text-sm md:text-base text-fg"
              />
              {isSearchPending || isFetching ? (
                <Loader2
                  className="absolute right-4 top-1/2 -translate-y-1/2 h-5 w-5 animate-spin text-fg-subtle"
                  aria-hidden="true"
                />
              ) : null}
              {searchValue && (
                <button
                  onClick={clearSearch}
                  className="absolute inset-y-0 right-0 pr-4 flex items-center text-fg-subtle hover:text-fg-muted transition-colors"
                >
                  <X className="h-5 w-5 bg-surface-3 rounded-full p-1" />
                </button>
              )}
            </div>
          </div>
        </div>
      </div>

      <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-6 md:py-10">
        <div className="lg:hidden mb-4">
          <button 
            onClick={() => setShowMobileFilters(!showMobileFilters)}
            className="flex items-center justify-center gap-2 w-full py-3 bg-surface border border-line rounded-xl font-bold text-fg-muted shadow-sm"
          >
            <Filter className="w-4 h-4 text-brand" />
            {showMobileFilters ? "Hide Filters" : "Show Filters"}
          </button>
        </div>

        <div className="flex flex-col lg:flex-row gap-8">
          <aside className={`${showMobileFilters ? "block" : "hidden"} lg:block w-full lg:w-72 shrink-0 animate-in fade-in slide-in-from-top-4 lg:animate-none`}>
            <div className="bg-surface rounded-2xl border border-line p-6 sticky top-6 shadow-sm">
              <div className="flex items-center justify-between mb-6 pb-4 border-b">
                <div className="flex items-center gap-2">
                  <SlidersHorizontal className="w-5 h-5 text-brand" />
                  <h3 className="font-bold text-fg">Filter Talents</h3>
                </div>
                <button onClick={() => setShowMobileFilters(false)} className="lg:hidden p-1">
                  <X className="w-5 h-5 text-fg-subtle" />
                </button>
              </div>
              
              <div className="space-y-6">
                <div className="space-y-2">
                  <label htmlFor="ft-category" className="text-[10px] font-bold text-fg-subtle uppercase tracking-widest">Category</label>
                  <select
                    id="ft-category"
                    value={categoryId}
                    onChange={(e) => setCategoryId(e.target.value)}
                    className="w-full h-11 px-3 rounded-xl border border-line bg-surface-2 text-sm outline-none"
                  >
                    <option value="">All Categories</option>
                    {(Array.isArray(categories) ? categories : categories?.results || [])?.map(cat => <option key={cat.id} value={cat.id}>{cat.name}</option>)}
                  </select>
                </div>
                <div className="space-y-2">
                  <label htmlFor="ft-min" className="text-[10px] font-bold text-fg-subtle uppercase tracking-widest">Budget Range ($)</label>
                  <div className="flex items-center gap-2">
                    <input
                      id="ft-min"
                      type="number"
                      min="0"
                      value={minPrice}
                      onChange={(e) => setMinPrice(e.target.value)}
                      placeholder="Min"
                      aria-invalid={budgetError ? 'true' : undefined}
                      className="w-full h-11 px-3 rounded-xl border border-line bg-surface-2 text-sm outline-none"
                    />
                    <input
                      type="number"
                      min="0"
                      value={maxPrice}
                      onChange={(e) => setMaxPrice(e.target.value)}
                      placeholder="Max"
                      aria-invalid={budgetError ? 'true' : undefined}
                      className="w-full h-11 px-3 rounded-xl border border-line bg-surface-2 text-sm outline-none"
                    />
                  </div>
                  {/* The reason the filter did not apply, shown next to the filter
                      rather than in an alert() the user has already dismissed. */}
                  {budgetError ? (
                    <p role="alert" className="text-xs font-medium text-danger-soft-fg">
                      {budgetError}
                    </p>
                  ) : null}
                </div>
                <div className="flex flex-col gap-2 pt-2">
                  <button onClick={handleApplyFilters} className="w-full py-3 bg-brand text-brand-fg text-sm font-bold rounded-xl hover:bg-brand-hover transition-all shadow-md">Apply Filters</button>
                  <button onClick={resetFilters} className="w-full py-3 text-sm font-semibold text-fg-subtle hover:bg-surface-3 rounded-xl">Reset</button>
                </div>
              </div>
            </div>
          </aside>

          <div className="flex-1">
            <div className="mb-6 flex items-center justify-between px-1">
              <p className="text-fg-subtle text-xs md:text-sm">
                Found <span className="font-bold text-fg">{data?.count || 0}</span> freelancers
              </p>
            </div>

            {isLoading ? (
              <div className="space-y-6">
                {[1, 2, 3].map((n) => <div key={n} className="h-64 lg:h-48 w-full bg-surface border border-line shimmer rounded-2xl" />)}
              </div>
            ) : (
              <div className="flex flex-col gap-6">
                {data?.results?.map((freelancer) => (
                  <FreelancerCard key={freelancer.id} {...freelancer} name={freelancer.full_name} title={freelancer.tagline} description={freelancer.bio} imageUrl={freelancer.profile_photo} price={freelancer.starting_price} rating={freelancer.average_rating} />
                ))}
                {data?.results?.length === 0 && (
                  /* Contextual. With a search or filter applied, an empty result
                     means nothing matched -- not that no freelancers exist. */
                  <EmptyState
                    variant={hasActiveFilters ? "no-match" : "first-use"}
                    title={hasActiveFilters ? "No freelancers match your search" : undefined}
                    onClearFilters={resetFilters}
                    className="bg-surface rounded-3xl border border-dashed border-line-strong mx-1"
                  />
                )}

                {/* Replaces a hand-rolled prev/next bar whose counter read
                    `results.length` -- this page's row count, not the range start. */}
                <Pagination
                  page={page}
                  pageSize={pageSize}
                  count={data?.count ?? 0}
                  onPageChange={setPage}
                  onPageSizeChange={setPageSize}
                  itemNoun="freelancers"
                  className="mt-4 pb-10"
                />
              </div>
            )}
          </div>
        </div>
      </div>
    </div>
  );
}
