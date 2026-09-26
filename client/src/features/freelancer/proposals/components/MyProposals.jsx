import React from "react";
import { ChevronDown } from "lucide-react";
import { ProposalCard } from "./ProposalsCard";
import { useMyProposals } from "./proposalsQueries";
import Pagination from "../../../../components/common/Pagination";
import EmptyState from "../../../../components/common/EmptyState";
import { useListState, toQueryParams } from "../../../../hooks/useListState";

export default function MyProposals() {
  // Status, page and page size live in the URL, so a filtered view is shareable,
  // Back steps back through filter changes rather than pages, and a refresh keeps
  // the user where they were.
  const {
    page,
    pageSize,
    filters,
    set: setFilter,
    setPage,
    setPageSize,
    hasActiveFilters,
    resetFilters,
  } = useListState({ defaults: { status: "" } });

  const status = filters.status;

  const { data, isLoading, isError } = useMyProposals(
    toQueryParams({ status }, page, pageSize),
  );

  if (isLoading) {
    return (
      <div className="min-h-screen flex items-center justify-center">
        <div className="animate-spin rounded-full h-8 w-8 border-b-2 border-blue-500"></div>
      </div>
    );
  }

  if (isError) {
    return (
      <EmptyState
        variant="error"
        title="Could not load your proposals"
        description="Something went wrong fetching your proposals. This is a problem on our side."
      />
    );
  }

  const proposals = data?.results || [];
  const totalCount = data?.count || 0;

  // `set` drops `page` alongside the filter, so this is the whole of
  // "changing a filter returns to page 1".
  const handleStatusChange = (e) => setFilter("status", e.target.value);

  return (
    <div className="min-h-screen bg-surface">
      <div className="max-w-page-lg mx-auto px-4 sm:px-6 py-8 sm:py-12">
        <div className="mb-8 sm:mb-10">
          <h1 className="text-2xl sm:text-[31px] font-bold text-fg leading-tight sm:leading-[40px] mb-2">
            My Proposals
          </h1>
          <p className="text-sm text-fg-muted">
            Review all the proposals you've submitted.
          </p>
        </div>

        <div className="flex flex-col sm:flex-row items-start sm:items-center justify-between gap-4 mb-6 sm:mb-8">
          <div className="flex items-center gap-3 sm:gap-4 flex-wrap">
            <div className="relative">
              <select 
                value={status} 
                onChange={handleStatusChange}
                className="appearance-none h-[42px] pl-4 pr-10 rounded-xl border border-line bg-surface text-sm font-medium text-fg-muted outline-none hover:bg-surface-2 cursor-pointer"
              >
                <option value="">All Status</option>
                <option value="pending">Pending</option>
                <option value="accepted">Accepted</option>
                <option value="rejected">Rejected</option>
              </select>
              <ChevronDown className="w-4 h-4 text-fg-subtle absolute right-3 top-1/2 -translate-y-1/2 pointer-events-none" />
            </div>
          </div>

          <div className="text-xs text-fg-subtle font-medium">
            <span>{proposals.length}</span>
            <span> {proposals.length === 1 ? 'proposal' : 'proposals'}</span>
          </div>
        </div>
        
        <div className="space-y-4">
          {proposals.length > 0 ? (
            proposals.map((proposal) => (
              <ProposalCard key={proposal.id} proposal={proposal} />
            ))
          ) : (
            <EmptyState
              variant={hasActiveFilters ? "no-match" : "first-use"}
              title={
                hasActiveFilters
                  ? "No proposals with this status"
                  : undefined
              }
              onClearFilters={resetFilters}
              className="border-2 border-dashed border-line-soft rounded-2xl"
            />
          )}
        </div>

        {/* Replaces a hand-rolled prev/next bar whose counter read
            `proposals.length` -- this page's row count, not the range start. */}
        <Pagination
          page={page}
          pageSize={pageSize}
          count={totalCount}
          onPageChange={setPage}
          onPageSizeChange={setPageSize}
          itemNoun="proposals"
          className="mt-8 pt-0"
        />
      </div>
    </div>
  );
}