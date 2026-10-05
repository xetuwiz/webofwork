import { useState } from "react";
import { Search, Filter, ChevronDown, X } from "lucide-react";
import { useAdminUsers } from "./usersQueries";
import UserTable from "./userTable";
import EmptyState from "../../../components/common/EmptyState";
import { useListState, toQueryParams } from "../../../hooks/useListState";

export default function AdminUserList() {
  // Page, page size, the search term and the role filter all live in the URL, so
  // the view is shareable, Back steps back through filter changes rather than
  // pages, and a refresh keeps the user where they were.
  const {
    page,
    pageSize,
    filters,
    set: setFilter,
    setPage,
    setPageSize,
    hasActiveFilters,
    resetFilters,
  } = useListState({ defaults: { search: "", status: "" } });

  const activeSearch = filters.search;
  const status = filters.status;

  // The raw input is what the field shows. It is seeded from the URL so a shared
  // link shows the term it was shared with.
  const [searchInput, setSearchInput] = useState(activeSearch);

  // Re-sync when the URL changes underneath us (Back/Forward, or a reset), which
  // is the "adjust state during render" pattern React documents for exactly this.
  // An effect would be a second render pass and trips react-hooks/set-state-in-
  // effect; this also avoids a visible frame of the stale term.
  const [lastSynced, setLastSynced] = useState(activeSearch);
  if (activeSearch !== lastSynced) {
    setLastSynced(activeSearch);
    setSearchInput(activeSearch);
  }

  // Search here is submit-driven (Enter or the button), not debounced. That is
  // deliberate: an admin searching by exact email does not want seven requests as
  // they type, and the button makes the query explicit. The submitted term is
  // what goes in the URL.
  const submitSearch = () => setFilter("search", searchInput.trim());

  const handleKeyDown = (e) => {
    if (e.key === "Enter") submitSearch();
  };

  const { data, isLoading, isError } = useAdminUsers(
    toQueryParams({ search: activeSearch, status }, page, pageSize),
  );

  const clearSearch = () => {
    setSearchInput("");
    setFilter("search", "");
  };

  const rows = data?.results ?? [];

  return (
    <div className="max-w-7xl mx-auto">
      <div className="flex flex-col md:flex-row md:items-end justify-between gap-4 mb-8">
        <div>
          <h2 className="text-2xl font-bold text-fg tracking-tight">User Management</h2>
          <p className="text-sm text-fg-subtle mt-1">
            Overview of all accounts, permissions, and platform activity.
          </p>
        </div>
      </div>

      <div className="bg-surface p-4 rounded-xl border border-line shadow-sm flex flex-wrap items-center gap-4 mb-6">
        <div className="flex flex-1 min-w-60 gap-2">
          <div className="relative flex-1">
            <Search className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-fg-subtle" />
            <input
              value={searchInput}
              onChange={(e) => setSearchInput(e.target.value)}
              onKeyDown={handleKeyDown}
              placeholder="Search by name or email..."
              aria-label="Search users by name or email"
              className="w-full h-11 pl-10 pr-10 bg-surface-2 border-none rounded-lg text-sm focus-visible:ring-2 focus-visible:ring-brand transition-all"
            />
            {searchInput && (
              <button
                type="button"
                onClick={clearSearch}
                aria-label="Clear search"
                className="absolute right-3 top-1/2 -translate-y-1/2 p-1 hover:bg-surface-inset rounded-full transition-colors"
              >
                <X className="w-3.5 h-3.5 text-fg-subtle" />
              </button>
            )}
          </div>

          <button
            type="button"
            onClick={submitSearch}
            aria-label="Run search"
            className="h-11 px-6 bg-brand hover:bg-brand-hover text-brand-fg rounded-lg text-sm font-semibold transition-colors flex items-center gap-2"
          >
            <Search className="w-4 h-4" />
          </button>
        </div>

        <div className="relative">
          <Filter className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-fg-subtle" />
          <select
            value={status}
            onChange={(e) => setFilter("status", e.target.value)}
            aria-label="Filter users by status"
            className="h-11 pl-10 pr-10 bg-surface-2 border-none rounded-lg text-sm focus-visible:ring-2 focus-visible:ring-brand appearance-none cursor-pointer font-medium text-fg-muted min-w-[160px]"
          >
            <option value="">All Users</option>
            <option value="active">Active Only</option>
            <option value="both">Both (Active)</option>
            <option value="client">Clients (Active)</option>
            <option value="blocked">Blocked</option>
            <option value="deleted">Deleted</option>
          </select>
          <ChevronDown className="absolute right-3 top-1/2 -translate-y-1/2 w-4 h-4 text-fg-subtle pointer-events-none" />
        </div>
      </div>

      {isLoading ? (
        <div className="flex flex-col items-center justify-center py-20 bg-surface rounded-xl border border-dashed border-line-strong">
          <div className="animate-spin rounded-full h-8 w-8 border-b-2 border-blue-600 mb-4"></div>
          <p className="text-sm text-fg-subtle font-medium">Fetching users...</p>
        </div>
      ) : isError ? (
        // An error is not an empty list: it means the answer is unknown, so it
        // gets its own EmptyState variant rather than the "no users" copy.
        <EmptyState
          variant="error"
          title="Could not load users"
          description="The user database could not be fetched. This is a problem on our side."
        />
      ) : rows.length === 0 ? (
        <EmptyState
          variant={hasActiveFilters ? "no-match" : "first-use"}
          title={hasActiveFilters ? "No users match your filters" : undefined}
          description={
            hasActiveFilters
              ? undefined
              : "Once accounts are registered they will appear here."
          }
          onClearFilters={resetFilters}
        />
      ) : (
        <UserTable
          users={rows}
          page={page}
          pageSize={pageSize}
          onPageChange={setPage}
          onPageSizeChange={setPageSize}
          totalCount={data?.count ?? 0}
        />
      )}
    </div>
  );
}
