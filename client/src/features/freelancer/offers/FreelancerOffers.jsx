import { Tag, Calendar, Banknote, ArrowRight, FileCheck } from "lucide-react";
import { useMyOffers } from "./offersQueries";
import { useModal } from "../../../hooks/modal/useModalStore";
import { formatDateDMY } from "../../../utils/date";
import Pagination from "../../../components/common/Pagination";
import EmptyState from "../../../components/common/EmptyState";
import { useListState, toQueryParams } from "../../../hooks/useListState";

export default function FreelancerOffers() {
  // Page and page size live in the URL, so a specific view of the offer queue is
  // shareable and a refresh keeps the freelancer where they were.
  const { page, pageSize, setPage, setPageSize } = useListState();

  const { data: offers, isLoading, isError } = useMyOffers(
    toQueryParams({}, page, pageSize),
  );
  const { openModal } = useModal();

  if (isLoading) return <div className="p-10 text-center">Loading offers...</div>;
  // An error is not an empty list: the answer is unknown, not "no offers".
  if (isError) {
    return (
      <EmptyState
        variant="error"
        title="Could not load your offers"
        description="Something went wrong fetching the offer queue. This is a problem on our side."
      />
    );
  }

  const offerList = offers?.results ?? [];
  const totalCount = offers?.count ?? 0;

  return (
    <div className="min-h-screen bg-surface-2/50 p-4 sm:p-8">
      <div className="max-w-5xl mx-auto">
        <div className="mb-8">
          <h1 className="text-2xl font-bold text-fg">Project Offers</h1>
          <p className="text-fg-muted">Review and sign contracts to start working.</p>
        </div>

        <div className="grid gap-4">
          {offerList.length > 0 ? (
            <>
              {offerList.map((offer) => (
                <div key={offer.id} className="bg-surface border border-line rounded-2xl p-6 hover:shadow-md transition-shadow">
                  <div className="flex flex-col sm:flex-row justify-between items-start gap-4 mb-6">
                    <div>
                      <div className="flex items-center gap-2 mb-2">
                        <span className="bg-brand-soft text-brand text-[10px] font-bold uppercase px-2 py-1 rounded">
                          New Offer
                        </span>
                        <span className="text-xs text-fg-subtle font-medium">
                          Received {formatDateDMY(offer.client_signed_at)}
                        </span>
                      </div>
                      <h3 className="text-lg font-bold text-fg">{offer.project_title}</h3>
                      <p className="text-sm text-fg-subtle">From {offer.client_name}</p>
                    </div>

                    <div className="text-right">
                      <p className="text-xs text-fg-subtle mb-1">Total Budget</p>
                      <p className="text-xl font-bold text-fg">${Number(offer.total_amount).toLocaleString()}</p>
                    </div>
                  </div>

                  <div className="flex flex-wrap items-center gap-6 py-4 border-t border-b border-line-soft mb-6">
                    <div className="flex items-center gap-2">
                      <Banknote className="w-4 h-4 text-fg-subtle" />
                      <span className="text-sm text-fg-muted font-medium">Fully Funded</span>
                    </div>
                    <div className="flex items-center gap-2">
                      <FileCheck className="w-4 h-4 text-fg-subtle" />
                      <span className="text-sm text-fg-muted font-medium">Client Signed</span>
                    </div>
                  </div>

                  <div className="flex justify-end">
                    <button 
                      onClick={() => openModal('offer-modal', { offer })}
                      className="flex items-center gap-2 bg-brand text-brand-fg px-6 py-2.5 rounded-xl font-semibold hover:bg-inverse transition-colors group"
                    >
                      <span>Review & Sign</span>
                      <ArrowRight className="w-4 h-4 group-hover:translate-x-1 transition-transform" />
                    </button>
                  </div>
                </div>
                    ))}
              {/* Replaces a hand-rolled prev/next bar guarded by
                  `offers.count > 10`, which hardcoded the page size -- at
                  page_size=50 the footer vanished even with several pages left.
                  The shared component derives that from count/pageSize. */}
              <Pagination
                page={page}
                pageSize={pageSize}
                count={totalCount}
                onPageChange={setPage}
                onPageSizeChange={setPageSize}
                itemNoun="offers"
                className="mt-8 pt-0"
              />
            </>
          ) : (
            <EmptyState
              variant="first-use"
              title="No pending offers"
              description="When a client sends you a contract to review, it will appear here."
              className="bg-surface rounded-2xl border-2 border-dashed border-line"
            />
          )}
        </div>
      </div>
    </div>
  );
}