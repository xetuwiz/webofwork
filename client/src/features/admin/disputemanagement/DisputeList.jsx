import { useEffect, useState } from "react";
import { Search, Loader2, User } from "lucide-react";
import { useAdminDisputes } from "./disputeQueries";
import { useNavigate } from "react-router-dom";
import { useDebouncedValue } from "../../../hooks/useDebounce";
import { useListState, toQueryParams } from "../../../hooks/useListState";
import Pagination from "../../../components/common/Pagination";
import EmptyState from "../../../components/common/EmptyState";

export default function DisputeList() {
  const navigate = useNavigate();
  // Page, page size and the status filter all live in the URL, so the view is
  // shareable, the back button steps back through filter changes rather than
  // through pages, and a refresh keeps the user where they were.
  const {
    page,
    pageSize,
    filters,
    set: setFilter,
    setPage,
    setPageSize,
    hasActiveFilters,
    resetFilters,
  } = useListState({ defaults: { status: "All", search: "" } });
  const activeFilter = filters.status || "All";

  // The raw input is what the field shows; the debounced copy is what the query
  // is keyed on, so typing does not fire a request per keystroke. Seeded from
  // the URL so a shared link shows the term it was shared with.
  const [searchInput, setSearchInput] = useState(filters.search);
  const [searchQuery, isSearchPending] = useDebouncedValue(searchInput, 400);

  const { data: disputes, isLoading, isFetching, isError } = useAdminDisputes(
    toQueryParams(
      { search: searchQuery, status: activeFilter === "All" ? "" : activeFilter },
      page,
      pageSize,
    ),
  );

  // Push the debounced term into the URL so it is shareable. Debounced first,
  // then written once, rather than on every keystroke -- otherwise Back would
  // step back one character at a time.
  useEffect(() => {
    if (searchQuery.trim() !== (filters.search ?? "")) {
      setFilter("search", searchQuery.trim());
    }
  }, [searchQuery, filters.search, setFilter]);

  const getStatusLabel = (status) => {
    return status.replace("_", " ").toUpperCase();
  };

  const getStatusStyles = (status) => {
    switch (status) {
      case "pending":
        return "bg-surface-3 text-fg-muted border-line";
      case "resolved":
        return "bg-emerald-50 text-emerald-600 border-emerald-200";
      default:
        return "bg-surface-3 text-fg-muted";
    }
  };

  // Writes through to the URL. `useListState.set` drops `page` alongside the
  // filter, so this is the whole of "changing a filter returns to page 1".
  const handleFilterChange = (filter) => setFilter("status", filter);

  if (isLoading) return (
    <div className="min-h-screen flex items-center justify-center bg-surface">
        <Loader2 className="w-8 h-8 animate-spin text-brand" />
    </div>
  );

  if (isError) return (
    <div className="min-h-screen flex items-center justify-center text-brand font-bold bg-surface">
      Error loading disputes. Please check your connection.
    </div>
  );

  return (
    <div className="min-h-screen bg-surface p-8">
      <div className="max-w-page-2xl mx-auto">
        
        <div className="mb-8 flex justify-between items-end">
          <div>
            <h1 className="text-2xl font-bold text-fg tracking-tight">Dispute Resolution Center</h1>
            <p className="text-sm text-fg-subtle mt-1">Manage and resolve conflicts between users</p>
          </div>
          <div className="text-right">
            <span className="text-2xl font-bold text-brand">{disputes?.count || 0}</span>
            <p className="text-[10px] font-bold text-fg-subtle uppercase tracking-widest">Total Cases</p>
          </div>
        </div>

        <div className="grid grid-cols-1 md:grid-cols-4 gap-4 mb-8">
          <div className="md:col-span-3 relative">
            <input
              type="text"
              placeholder="Search by ID, reason or description..."
              value={searchInput}
              onChange={(e) => {
                setSearchInput(e.target.value);
                setPage(1);
              }}
              aria-label="Search disputes"
              className="w-full h-12 pl-12 pr-12 py-2 text-sm border border-line rounded-xl focus-visible:ring-2 focus-visible:ring-primary/20 focus-visible:border-primary outline-none transition-all shadow-sm"
            />
            <Search className="absolute left-4 top-1/2 -translate-y-1/2 w-5 h-5 text-fg-subtle" />
            {/* Says "still working" while the debounce is pending, so a slow
                response never looks like a search that found nothing. */}
            {isSearchPending || isFetching ? (
              <Loader2
                className="absolute right-4 top-1/2 -translate-y-1/2 h-4 w-4 animate-spin text-fg-subtle"
                aria-hidden="true"
              />
            ) : null}
          </div>
          
        </div>

        <div className="flex gap-2 mb-8 overflow-x-auto pb-2">
          {["All", "pending", "resolved"].map((f) => (
            <button
              key={f}
              onClick={() => handleFilterChange(f)}
              className={`px-6 py-2 text-[11px] font-black rounded-full border transition-all whitespace-nowrap uppercase tracking-widest ${
                activeFilter === f 
                  ? "bg-brand border-primary text-white shadow-md shadow-primary/20" 
                  : "bg-surface border-line text-fg-subtle hover:border-primary hover:text-brand"
              }`}
            >
              {f.replace("_", " ")}
            </button>
          ))}
        </div>

        <div className="bg-surface border border-line-soft rounded-3xl overflow-hidden shadow-xl shadow-gray-200/50">
          <div className="overflow-x-auto">
            <table className="w-full text-left border-collapse">
              <thead>
                <tr className="bg-surface-2/50">
                  <th className="px-8 py-5 text-[10px] font-black text-fg-subtle uppercase tracking-[0.2em]">Case ID</th>
                  <th className="px-8 py-5 text-[10px] font-black text-fg-subtle uppercase tracking-[0.2em]">Client</th>
                  <th className="px-8 py-5 text-[10px] font-black text-fg-subtle uppercase tracking-[0.2em]">Freelancer</th>
                  <th className="px-8 py-5 text-[10px] font-black text-fg-subtle uppercase tracking-[0.2em]">Dispute Reason</th>
                  <th className="px-8 py-5 text-[10px] font-black text-fg-subtle uppercase tracking-[0.2em]">Status</th>
                  <th className="px-8 py-5 text-right text-[10px] font-black text-fg-subtle uppercase tracking-[0.2em]">Action</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-gray-50">
                {disputes?.results?.map((dispute) => (
                  <tr key={dispute.id} className="hover:bg-brand-hover/[0.02] transition-colors group">
                    <td className="px-8 py-6">
                      <span className="text-sm font-bold text-fg bg-surface-3 px-3 py-1 rounded-lg">
                        #{dispute.id}
                      </span>
                      <div className="text-[10px] text-fg-subtle mt-2 font-medium">
                        {new Date(dispute.created_at).toLocaleDateString(undefined, { dateStyle: 'medium' })}
                      </div>
                    </td>
                    <td className="px-8 py-6">
                      <div className="flex flex-col gap-2">
                        <div className="flex items-center gap-2">
                          <div className="w-6 h-6 rounded-full bg-brand/10 flex items-center justify-center">
                            <User className="w-3 h-3 text-brand" />
                          </div>
                          <span className="text-sm font-semibold text-fg">{dispute.client_name}</span>
                        </div>
                      </div>
                    </td>
                    <td>
                        <div className="flex items-center gap-2">
                          <div className="w-6 h-6 rounded-full bg-surface-3 flex items-center justify-center">
                            <User className="w-3 h-3 text-fg-subtle" />
                          </div>
                          <span className="text-sm font-semibold text-fg">{dispute.freelancer_name}</span>
                        </div>
                    </td>
                    <td className="px-8 py-6">
                      <div className="max-w-xs">
                        <p className="text-sm text-fg-muted font-medium line-clamp-1 italic">
                          "{dispute.reason.replace("_", " ")}"
                        </p>
                        <p className="text-[11px] text-fg-subtle mt-1 line-clamp-1">{dispute.description}</p>
                      </div>
                    </td>
                    <td className="px-8 py-6">
                      <span className={`px-3 py-1.5 rounded-full text-[10px] font-black border tracking-wider ${getStatusStyles(dispute.status)}`}>
                        {getStatusLabel(dispute.status)}
                      </span>
                    </td>
                    <td className="px-8 py-6 text-right">
                      <button 
                        onClick={() => navigate(`/admin/disputes/${dispute.id}`)}
                        className="px-6 py-2 bg-surface border-2 border-primary text-brand text-[11px] font-black rounded-xl hover:bg-brand-hover hover:text-brand-fg transition-all shadow-sm active:scale-95"
                      >
                        VIEW CASE
                      </button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>

          {/* Contextual rather than one-size-fits-all. With a filter or a search
              term applied, an empty table means the filter is too narrow -- and
              "No disputes found" there would wrongly tell an admin there are no
              disputes at all. With nothing applied it is the first-use case. */}
          {disputes?.results?.length === 0 ? (
            <EmptyState
              variant={hasActiveFilters ? "no-match" : "first-use"}
              title={hasActiveFilters ? "No disputes match your filters" : undefined}
              onClearFilters={resetFilters}
            />
          ) : null}
        </div>

        {/* Replaces a hand-rolled prev/next bar. The old one read
            `disputes.results.length` -- this page's row count, not the range start
            -- so on page 3 of 10 it said "Showing 10 of 42". The shared component
            derives the real range from page / pageSize / count. */}
        <Pagination
          page={page}
          pageSize={pageSize}
          count={disputes?.count ?? 0}
          onPageChange={setPage}
          onPageSizeChange={setPageSize}
          itemNoun="disputes"
        />
      </div>
    </div>
  );
}
