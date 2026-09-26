import React from 'react';
import { useReceivedInvitations } from './invitationQueries';
import { InvitationCard } from './InvitationCard';
import { Loader2 } from 'lucide-react';
import Pagination from '../../../components/common/Pagination';
import EmptyState from '../../../components/common/EmptyState';
import { useListState, toQueryParams } from '../../../hooks/useListState';

export default function InvitationsPage() {
  // Tab, page and page size live in the URL, so a filtered view is shareable and
  // Back steps back through tab changes rather than pages.
  const {
    page,
    pageSize,
    filters,
    set: setFilter,
    setPage,
    setPageSize,
    hasActiveFilters,
    resetFilters,
  } = useListState({ defaults: { tab: 'all' } });

  const activeTab = filters.tab || 'all';

  const { data, isLoading, isError } = useReceivedInvitations(
    toQueryParams({ status: activeTab === 'all' ? '' : activeTab }, page, pageSize),
  );

  const invitations = data?.results || [];

  // Tab keys. `closed` is a UI grouping the server expands to declined +
  // cancelled -- the `InvitationStatus` enum has no such member.
  //
  // The fourth tab used to be `hired`, which does not exist in the enum or
  // anywhere else in the codebase. Clicking it sent `status=hired`, the server's
  // `EnumQueryParser` could not match it, the filter silently became "no
  // filter", and every invitation was listed under a "hired" heading.
  const TABS = ['all', 'pending', 'accepted', 'closed'];

  // `total_counts` is now actually sent by GET /projects/invitations/received.
  // It was never sent before: the field was read here, came back undefined, and
  // `|| 0` turned that into a confident "(0)" on every tab while the rows below
  // rendered normally -- which is what made this look like a data problem rather
  // than a missing field.
  const counts = data?.total_counts ?? {};

  if (isLoading) {
    return (
      <div className="min-h-screen flex items-center justify-center">
        <Loader2 className="w-10 h-10 text-brand animate-spin" />
      </div>
    );
  }

  // `isError` was destructured but never checked, so a failed request rendered
  // "No all invitations found" -- telling the user they had no invitations when
  // in fact the answer was unknown. An error is not an empty list.
  if (isError) {
    return (
      <EmptyState
        variant="error"
        title="Could not load your invitations"
        description="Something went wrong fetching the invitation list. This is a problem on our side."
      />
    );
  }

  return (
    <div className="min-h-screen bg-surface-2">
      <div className="max-w-page-lg mx-auto px-4 py-12">
        <div className="mb-8">
          <h1 className="text-2xl font-black text-fg">Invitations</h1>
          <p className="text-sm text-fg-muted mt-1">Manage project requests from clients.</p>
        </div>

        <div className="border-b border-line mb-8 flex gap-8">
          {TABS.map((tab) => (
            <button
              key={tab}
              // `set` drops `page` alongside the filter, so this is the whole of
              // "changing a tab returns to page 1".
              onClick={() => setFilter('tab', tab)}
              className={`pb-4 px-1 relative text-xs font-bold uppercase tracking-wider transition-all ${
                activeTab === tab ? 'text-brand' : 'text-fg-subtle hover:text-fg-muted'
              }`}
            >
              {tab} ({counts[tab] ?? 0})
              {activeTab === tab && (
                <div className="absolute bottom-0 left-0 right-0 h-1 bg-brand rounded-t-full"></div>
              )}
            </button>
          ))}
        </div>

        <div className="space-y-4">
          {invitations.length > 0 ? (
            <>
              {invitations.map((invitation) => (
                <InvitationCard key={invitation.id} invitation={invitation} />
              ))}
              {/* Replaces a hand-rolled prev/next bar guarded by
                  `data.count > 10`, which hardcoded the page size. The old
                  counter also read `invitations.length` -- this page's row count,
                  not the range start. */}
              <Pagination
                page={page}
                pageSize={pageSize}
                count={data?.count ?? 0}
                onPageChange={setPage}
                onPageSizeChange={setPageSize}
                itemNoun="invitations"
                className="mt-8 pt-0"
              />
            </>
          ) : (
            <EmptyState
              variant={hasActiveFilters ? 'no-match' : 'first-use'}
              title={
                hasActiveFilters
                  ? `No ${activeTab} invitations found`
                  : undefined
              }
              onClearFilters={resetFilters}
              className="bg-surface rounded-2xl border border-dashed border-line-strong"
            />
          )}
        </div>
      </div>
    </div>
  );
}