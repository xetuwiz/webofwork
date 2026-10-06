import React, { useState } from 'react';
import JobCard from "./components/JobCard";
import { useFreelancerProjects } from "./findJobsQueries";
import { useAllCategories, useAllSkillls } from '../../client/projectManagement/projectQueries';
import { Link } from 'react-router-dom';
import Pagination from '../../../components/common/Pagination';
import EmptyState from '../../../components/common/EmptyState';
import { Popover, PopoverTrigger, PopoverContent, PopoverItem } from '../../../components/common/Popover';
import { useListState, toQueryParams } from '../../../hooks/useListState';

export default function FindJobs() {
  // All five filters plus paging live in the URL, so a saved search is a
  // shareable link and Back steps back through filter changes rather than pages.
  // This page previously held 11 useState values, including a `*Label` state per
  // dropdown kept in step with its value by hand -- a second source of truth that
  // drifts the moment one is set without the other.
  const {
    page,
    pageSize,
    filters,
    set: setFilter,
    setMany,
    setPage,
    setPageSize,
    hasActiveFilters,
    resetFilters,
  } = useListState({
    defaults: {
      search: "",
      category: "",
      skill: "",
      min_price: "",
      max_price: "",
      delivery_days: "",
    },
  });

  // Dropdown open/closed used to be four useState booleans, closed by hand from
  // every trigger and with no Escape handling. <Popover> owns that state now.

  const { data: skillsData, isLoading: skillsLoading } = useAllSkillls();

  const { data: categories } = useAllCategories();

  // The raw input is what the field shows; the committed term is what filters.
  // Seeded from the URL so a shared link shows the term it was shared with.
  const [searchInput, setSearchInput] = useState(filters.search);
  const [lastSynced, setLastSynced] = useState(filters.search);
  if (filters.search !== lastSynced) {
    setLastSynced(filters.search);
    setSearchInput(filters.search);
  }

  const { data, isLoading } = useFreelancerProjects(
    toQueryParams(
      {
        search: filters.search,
        category: filters.category,
        skill: filters.skill,
        min_price: filters.min_price,
        max_price: filters.max_price,
        delivery_days: filters.delivery_days,
      },
      page,
      pageSize,
    ),
  );

  const projects = data?.results || [];
  const totalCount = data?.count || 0;

  // Labels are DERIVED from the selected value rather than stored alongside it.
  // While the lookup lists are still loading, fall back to the raw value so the
  // button never reads "undefined" or goes blank.
  const categoryLabel =
    categories?.find?.((c) => String(c.id) === filters.category)?.name || filters.category;
  const skillLabel =
    skillsData?.find?.((s) => s.name === filters.skill)?.name || filters.skill;

  const handleSearchSubmit = (e) => {
    e.preventDefault();
    setFilter("search", searchInput.trim());
  };

  const clearSearch = () => {
    setSearchInput("");
    setFilter("search", "");
  };

  // `resetFilters` clears every filter key and the page in one write, which is
  // why the old version had to call eight setters in sequence and could leave
  // the page on 3 of a now-single-page result.
  const clearAllFilters = () => {
    setSearchInput("");
    resetFilters();
  };

  return (
    <div className="min-h-screen mt-5 bg-surface">
      <header className="bg-surface border-b border-line-soft py-12">
        <div className="max-w-7xl mx-auto px-6 flex flex-col md:flex-row justify-between items-center gap-8">
          <div className="text-left w-full">
            <h1 className="text-3xl font-bold text-fg">Find Your Next Project</h1>
            <p className="text-fg-subtle mt-2">Browse opportunities matched to your skills</p>
          </div>

          <form onSubmit={handleSearchSubmit} className="relative w-full max-md flex gap-2">
            <div className="relative grow">
              <div className="absolute left-4 top-1/2 -translate-y-1/2 text-fg-subtle">
                <svg width="18" height="18" viewBox="0 0 20 20" fill="none">
                  <path d="M17.5 17.5L13.8834 13.8838M9.16667 15.8333C12.8486 15.8333 15.8333 12.8486 15.8333 9.16667C15.8333 5.48477 12.8486 2.5 9.16667 2.5C5.48477 2.5 2.5 5.48477 2.5 9.16667C2.5 12.8486 5.48477 15.8333 9.16667 15.8333Z" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" />
                </svg>
              </div>
              <input
                type="text"
                value={searchInput}
                onChange={(e) => setSearchInput(e.target.value)}
                placeholder="Search for jobs..."
                className="w-full pl-12 pr-10 py-3 bg-surface-2 border border-line rounded-xl focus-visible:ring-2 focus-visible:ring-primary outline-none text-sm transition-all"
              />
              {searchInput && (
                <button type="button" onClick={clearSearch} className="absolute right-3 top-1/2 -translate-y-1/2 text-fg-subtle hover:text-fg-muted">
                  <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"><line x1="18" y1="6" x2="6" y2="18"></line><line x1="6" y1="6" x2="18" y2="18"></line></svg>
                </button>
              )}
            </div>
            <button type="submit" className="bg-brand hover:bg-brand-hover text-brand-fg px-6 py-3 rounded-xl text-sm font-bold transition-colors shadow-sm">Search</button>
          </form>
        </div>
      </header>

      <div className="bg-surface border-b border-line-soft py-4 sticky top-0 z-10">
        <div className="max-w-7xl mx-auto px-6 flex flex-wrap justify-between items-center gap-4">
          {/* These four were `useState` booleans plus a conditionally rendered
              absolutely-positioned div. None handled Escape, so a keyboard user
              could open a filter and have no way to dismiss it; none closed on an
              outside click either, and the cross-closing was hand-written into
              every trigger (`setIsSkillsOpen(false); setIsCategoryOpen(false);`
              ...). Radix does all of that per instance, including closing a
              sibling menu when another trigger is clicked. */}
          <div className="flex flex-wrap gap-3">
            <Popover>
              <PopoverTrigger
                className={`flex items-center gap-2 px-4 py-2 border rounded-xl text-sm font-medium transition-colors ${filters.category ? "border-primary bg-brand-soft text-brand" : "border-line text-fg-muted hover:bg-surface-2"}`}
              >
                <span>{categoryLabel || "Category"}</span>
                <svg
                  className="transition-transform group-data-[state=open]:rotate-180"
                  width="12" height="12" viewBox="0 0 16 16" fill="none" aria-hidden="true"
                >
                  <path d="M4 6L8 10L12 6" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" />
                </svg>
              </PopoverTrigger>
              <PopoverContent aria-label="Category" className="w-48 max-h-60 overflow-y-auto">
                <PopoverItem onClick={() => setFilter("category", "")} className="text-brand font-medium border-b border-line-soft rounded-none">
                  All Categories
                </PopoverItem>
                {categories.map((cat) => (
                  <PopoverItem key={cat.id} onClick={() => setFilter("category", String(cat.id))}>
                    {cat.name}
                  </PopoverItem>
                ))}
              </PopoverContent>
            </Popover>

            <Popover>
              <PopoverTrigger
                className={`flex items-center gap-2 px-4 py-2 border rounded-xl text-sm font-medium transition-colors ${filters.skill ? "border-primary bg-brand-soft text-brand" : "border-line text-fg-muted hover:bg-surface-2"}`}
              >
                <span>{skillLabel || "Skills"}</span>
                {skillsLoading ? (
                  <div className="w-3 h-3 border-2 border-line-strong border-t-blue-500 rounded-full animate-spin" aria-hidden="true" />
                ) : (
                  <svg
                    className="transition-transform group-data-[state=open]:rotate-180"
                    width="12" height="12" viewBox="0 0 16 16" fill="none" aria-hidden="true"
                  >
                    <path d="M4 6L8 10L12 6" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" />
                  </svg>
                )}
              </PopoverTrigger>
              <PopoverContent aria-label="Skill" className="w-56 max-h-60 overflow-y-auto">
                <PopoverItem onClick={() => setFilter("skill", "")} className="text-brand font-medium border-b border-line-soft rounded-none">
                  All Skills
                </PopoverItem>
                {skillsData.map((skill) => (
                  <PopoverItem key={skill.id} onClick={() => setFilter("skill", skill.name)}>
                    {skill.name}
                  </PopoverItem>
                ))}
              </PopoverContent>
            </Popover>

            <Popover>
              <PopoverTrigger
                className={`flex items-center gap-2 px-4 py-2 border rounded-xl text-sm font-medium transition-colors ${filters.max_price ? "border-primary bg-brand-soft text-brand" : "border-line text-fg-muted hover:bg-surface-2"}`}
              >
                <span>{filters.max_price ? `Up to $${filters.max_price}` : "Budget"}</span>
                <svg
                  className="transition-transform group-data-[state=open]:rotate-180"
                  width="12" height="12" viewBox="0 0 16 16" fill="none" aria-hidden="true"
                >
                  <path d="M4 6L8 10L12 6" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" />
                </svg>
              </PopoverTrigger>
              <PopoverContent aria-label="Budget" className="w-48">
                <PopoverItem
                  onClick={() => setMany({ min_price: "", max_price: "" })}
                  className="text-brand font-medium border-b border-line-soft rounded-none"
                >
                  All Budgets
                </PopoverItem>
                {[500, 1000, 5000, 10000, 50000].map((amount) => (
                  <PopoverItem
                    key={amount}
                    onClick={() => setMany({ min_price: "0", max_price: String(amount) })}
                  >
                    Up to ${amount}
                  </PopoverItem>
                ))}
              </PopoverContent>
            </Popover>

            <Popover>
              <PopoverTrigger
                className={`flex items-center gap-2 px-4 py-2 border rounded-xl text-sm font-medium transition-colors ${filters.delivery_days ? "border-primary bg-brand-soft text-brand" : "border-line text-fg-muted hover:bg-surface-2"}`}
              >
                <span>{filters.delivery_days ? `Under ${filters.delivery_days} days` : "Delivery"}</span>
                <svg
                  className="transition-transform group-data-[state=open]:rotate-180"
                  width="12" height="12" viewBox="0 0 16 16" fill="none" aria-hidden="true"
                >
                  <path d="M4 6L8 10L12 6" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" />
                </svg>
              </PopoverTrigger>
              <PopoverContent aria-label="Delivery time" className="w-48">
                <PopoverItem
                  onClick={() => setFilter("delivery_days", "")}
                  className="text-brand font-medium border-b border-line-soft rounded-none"
                >
                  Any Time
                </PopoverItem>
                {[3, 7, 14, 30].map((days) => (
                  <PopoverItem key={days} onClick={() => setFilter("delivery_days", String(days))}>
                    Under {days} days
                  </PopoverItem>
                ))}
              </PopoverContent>
            </Popover>
          </div>
        </div>
      </div>

      <div className="max-w-7xl mx-auto px-6 py-10">
        <p className="text-sm text-fg-subtle mb-8 font-medium">
          <span className="text-fg font-bold">{totalCount}</span> jobs found
          {filters.category && <span className="ml-2 bg-brand-soft text-brand px-2 py-0.5 rounded text-xs">Category: {categoryLabel}</span>}
          {filters.skill && <span className="ml-2 bg-surface-3 text-fg-muted px-2 py-0.5 rounded text-xs">Skill: {skillLabel}</span>}
          {filters.max_price && <span className="ml-2 bg-success-soft text-success px-2 py-0.5 rounded text-xs">Max: ${filters.max_price}</span>}
          {filters.delivery_days && <span className="ml-2 bg-orange-100 text-orange-700 px-2 py-0.5 rounded text-xs">{filters.delivery_days} days</span>}
        </p>

        {isLoading ? (
          <div className="p-20 text-center text-fg-subtle">Loading jobs...</div>
        ) : (
          <>
            <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-2 gap-8">
              {projects.length > 0 ? (
                projects.map((job) => (
                  <Link key={job.id} to={`/freelancer/jobs/${job.id}`}>
                    <JobCard
                      id={job.id}
                      category={job.category_name}
                      title={job.title}
                      description={job.description}
                      priceRange={job.pricing_type === 'fixed' ? `$${job.fixed_price}` : `$${job.min_budget}-$${job.max_budget}`}
                      duration={`${job.delivery_days} days`}
                      skills={job.skills || []}
                    />
                  </Link>
                ))
              ) : (
                /* Contextual. With any filter applied, an empty grid means the
                   filters are too narrow -- "No projects found" would wrongly
                   tell a freelancer the marketplace is empty. */
                <div className="col-span-full">
                  <EmptyState
                    variant={hasActiveFilters ? "no-match" : "first-use"}
                    title={
                      hasActiveFilters
                        ? "No projects match your filters"
                        : undefined
                    }
                    onClearFilters={clearAllFilters}
                    className="border-2 border-dashed border-line-soft rounded-3xl"
                  />
                </div>
              )}
            </div>

            {/* Replaces a hand-rolled prev/next bar guarded by
                `totalCount > 10`, which hardcoded the page size, and whose
                counter read `projects.length` -- this page's row count rather
                than the range start. */}
            <Pagination
              page={page}
              pageSize={pageSize}
              count={totalCount}
              onPageChange={setPage}
              onPageSizeChange={setPageSize}
              itemNoun="projects"
              className="mt-8 pt-0"
            />
          </>
        )}
      </div>
    </div>
  );
}
