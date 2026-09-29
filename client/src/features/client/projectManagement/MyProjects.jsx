import React, { useState } from "react";
import ProjectCard from "./components/ProjectCard";
import { useMyProjects } from "./projectQueries";
import { Briefcase, CircleCheck, Hourglass, Search, X } from "lucide-react";
import Pagination from "../../../components/common/Pagination";
import EmptyState from "../../../components/common/EmptyState";
import { useListState, toQueryParams } from "../../../hooks/useListState";

export default function MyProjects() {
  // Tab, page, page size and the search term all live in the URL, so a filtered
  // view is shareable and Back steps back through tab changes rather than pages.
  const {
    page,
    pageSize,
    filters,
    set: setFilter,
    setPage,
    setPageSize,
    hasActiveFilters,
    resetFilters,
  } = useListState({ defaults: { tab: "all", search: "" } });

  const activeTab = filters.tab || "all";

  // Controlled input, seeded from the URL. This used to be an uncontrolled
  // field read through a ref on submit, so a shared link could not prefill it and
  // clearing the filter left the box's text out of sync with the query.
  const [searchInput, setSearchInput] = useState(filters.search);
  const [lastSynced, setLastSynced] = useState(filters.search);
  if (filters.search !== lastSynced) {
    setLastSynced(filters.search);
    setSearchInput(filters.search);
  }

  const { data, isLoading } = useMyProjects(
    toQueryParams(
      { status: activeTab === "all" ? "" : activeTab, search: filters.search },
      page,
      pageSize,
    ),
  );

  const jobs = data?.results ?? [];
  const totalCount = data?.count ?? 0;
  const counts = data?.counts ?? { all: 0, completed: 0, in_progress: 0, open: 0 };

  const handleSearch = () => setFilter("search", searchInput.trim());

  const clearSearch = () => {
    setSearchInput("");
    setFilter("search", "");
  };

  const handleKeyDown = (e) => {
    if (e.key === 'Enter') handleSearch();
  };

  const TabButton = ({ id, label, Icon }) => (
    <button
      type="button"
      onClick={() => setFilter("tab", id)}
      aria-current={activeTab === id ? "true" : undefined}
      className={`flex items-center justify-center gap-2 px-2 md:px-1 py-3 md:py-4 border-b-2 transition-all flex-1 md:flex-none whitespace-nowrap ${
        activeTab === id ? "border-primary text-brand" : "border-transparent text-fg-subtle hover:text-fg-muted"
      }`}
    >
      <Icon className="w-4 h-4 md:w-5 md:h-5" />
      <span className="text-xs md:text-sm font-semibold">{label}</span>
      <span className={`px-1.5 py-0.5 rounded-full text-[10px] md:text-xs font-bold ${
        activeTab === id ? "bg-brand-soft text-brand" : "bg-surface-3 text-fg-subtle"
      }`}>
        {counts[id] || 0}
      </span>
    </button>
  );

  return (
    <div className="min-h-screen bg-surface-2" style={{ fontFamily: "Roboto, sans-serif" }}>
      <div className="max-w-page-3xl mx-auto px-4 md:px-8 py-6 md:py-10">
        
        <div className="flex flex-col lg:flex-row lg:justify-between lg:items-center gap-6 mb-8">
          <div className="flex flex-col gap-1.5">
            <h1 className="text-2xl md:text-3xl font-extrabold text-fg tracking-tight">My Job Posts</h1>
            <p className="text-sm md:text-base text-fg-subtle font-medium">Manage and track your active projects.</p>
          </div>

          <div className="relative flex w-full lg:w-[420px] items-center shadow-sm">
            <div className="relative flex-1">
              <Search className="absolute left-4 top-1/2 -translate-y-1/2 text-fg-subtle w-5 h-5" />
              <input
                type="text"
                value={searchInput}
                onChange={(e) => setSearchInput(e.target.value)}
                onKeyDown={handleKeyDown}
                placeholder="Search by title..."
                aria-label="Search your projects by title"
                className="w-full h-[50px] pl-11 pr-10 border border-line rounded-l-xl text-sm md:text-base focus-visible:ring-2 focus-visible:ring-brand/20 focus-visible:border-brand outline-none transition-all"
              />
              {filters.search && (
                <button
                  onClick={clearSearch}
                  className="absolute right-3 top-1/2 -translate-y-1/2 text-fg-subtle hover:text-fg-subtle"
                >
                  <X className="w-5 h-5" />
                </button>
              )}
            </div>
            <button
              onClick={handleSearch}
              className="h-[50px] px-6 bg-brand hover:bg-brand-hover text-brand-fg rounded-r-xl font-bold transition-all active:scale-95"
            >
              <Search className="w-5 h-5 " />
            </button>
          </div>
        </div>

        <div className="bg-surface rounded-t-xl md:bg-transparent border-b border-line mb-8 overflow-x-auto no-scrollbar">
          <nav className="flex md:gap-8 px-2 md:px-0 min-w-max md:min-w-0">
            <TabButton id="all" label="All Posts" Icon={Briefcase} />
            <TabButton id="completed" label="Completed" Icon={CircleCheck} />
            <TabButton id="in_progress" label="In Progress" Icon={Hourglass} />
          </nav>
        </div>

        <div className="space-y-4 md:space-y-6">
          {isLoading ? (
            [...Array(3)].map((_, i) => <div key={i} className="h-44 bg-surface border border-line-soft shimmer rounded-2xl shadow-sm" />)
          ) : jobs.length > 0 ? (
            jobs.map((job) => <ProjectCard key={job.id} job={job} />)
          ) : (
            /* Contextual: with a tab or search applied, an empty list means the
               filter is too narrow -- "No results found" would be misleading. */
            <EmptyState
              variant={hasActiveFilters ? "no-match" : "first-use"}
              title={hasActiveFilters ? "No projects match your filters" : undefined}
              onClearFilters={resetFilters}
              className="bg-surface rounded-2xl border border-line-soft shadow-sm"
            />
          )}
        </div>

        {/* Replaces a hand-rolled prev/next bar whose counter read `jobs.length`
            -- this page's row count, not the range start. */}
        <Pagination
          page={page}
          pageSize={pageSize}
          count={totalCount}
          onPageChange={setPage}
          onPageSizeChange={setPageSize}
          itemNoun="projects"
          className="mt-10 pb-8"
        />
      </div>
    </div>
  );
}
