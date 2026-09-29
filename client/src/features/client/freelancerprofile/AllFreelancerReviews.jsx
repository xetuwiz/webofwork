import React from "react";
import { useParams, useNavigate } from "react-router-dom";
import { Star, ArrowLeft, MessageSquare } from "lucide-react";
import { useFreelancerReviews } from "./freelancerProfileQueries";
import { formatDateDMY } from "../../../utils/date";
import { useListState } from "../../../hooks/useListState";
import EmptyState from "../../../components/common/EmptyState";
import { PAGE_SIZE_OPTIONS } from "../../../utils/pagination";

export default function AllFreelancerReviews() {
  const { id } = useParams();
  const navigate = useNavigate();

  // Page and page size live in the URL, so "loaded through page 3" is
  // shareable. Note this page uses a Load-more pattern rather than prev/next:
  // the user has already read pages 1..N, so swapping in the shared <Pagination>
  // (which replaces the page) would be wrong -- it would discard what they read.
  const { page, pageSize, setPage, setPageSize } = useListState();

  const { data, isLoading, isError, isFetching } = useFreelancerReviews(id, {
    page,
    page_size: pageSize,
  });

  // Pages accumulate as the user loads more, rather than replacing each other.
  // `loadedThrough` makes that safe under URL navigation too: jumping backwards
  // (or straight to page 5) replaces the list instead of appending a page the
  // user has already seen or skipped over.
  const [allReviews, setAllReviews] = React.useState([]);
  const [loadedThrough, setLoadedThrough] = React.useState(0);

  React.useEffect(() => {
    if (!data?.results) return;
    setAllReviews((prev) => (page > loadedThrough ? [...prev, ...data.results] : data.results));
    setLoadedThrough((prev) => Math.max(prev, page));
  }, [data, page, loadedThrough]);

  if (isLoading && page === 1 && allReviews.length === 0) return (
    <div className="min-h-screen flex items-center justify-center">
      <div className="animate-spin rounded-full h-12 w-12 border-4 border-primary border-t-transparent" />
    </div>
  );

  // `isError` was destructured but never checked, so a failed request fell
  // through to "No reviews found" -- telling a visitor this freelancer has no
  // reviews when the answer was actually unknown.
  if (isError) {
    return (
      <EmptyState
        variant="error"
        title="Could not load reviews"
        description="Something went wrong fetching the reviews. This is a problem on our side."
      />
    );
  }

  return (
    <div className="min-h-screen bg-surface-2 py-12">
      <div className="max-w-3xl mx-auto px-4">
        <button 
          onClick={() => navigate(-1)}
          className="flex items-center gap-2 text-[10px] font-black uppercase tracking-widest text-fg-subtle hover:text-fg mb-8 transition-colors"
        >
          <ArrowLeft size={14} /> Back to Profile
        </button>

        <div className="flex items-center justify-between mb-10">
          <h1 className="text-3xl font-black text-fg tracking-tight">All Reviews</h1>
          <div className="px-4 py-2 bg-surface rounded-xl border border-line shadow-sm">
            <span className="text-lg font-black text-fg">{data?.count || 0}</span>
            <span className="ml-2 text-[10px] font-black text-fg-subtle uppercase tracking-widest">Total</span>
          </div>
        </div>

        <div className="space-y-6">
          {allReviews.map((review, i) => (
            <div key={`${review.id}-${i}`} className="bg-surface rounded-2xl p-8 border border-line shadow-sm transition-all">
              <div className="flex justify-between items-start mb-4">
                <div className="flex items-center gap-4">
                  {review.reviewer_photo ? (
                    <img src={review.reviewer_photo} className="w-12 h-12 rounded-xl object-cover" alt="" />
                  ) : (
                    <div className="w-12 h-12 bg-surface-3 rounded-xl flex items-center justify-center font-black text-fg-subtle text-sm">
                      {review.reviewer_name[0]}
                    </div>
                  )}
                  <div>
                    <h4 className="font-black text-fg text-sm tracking-tight">{review.reviewer_name}</h4>
                    <div className="flex gap-0.5 mt-1">
                      {[...Array(5)].map((_, starIndex) => (
                        <Star
                          key={starIndex}
                          size={12}
                          className={starIndex < review.rating ? "text-warning fill-yellow-400" : "text-fg-subtle"}
                        />
                      ))}
                    </div>
                  </div>
                </div>
                <div className="text-right">
                  <span className="block text-[10px] font-black text-fg-subtle uppercase tracking-widest">{review.project_title}</span>
                  <span className="text-[11px] font-bold text-fg-subtle">{formatDateDMY(review.created_at)}</span>
                </div>
              </div>
              <p className="text-sm text-fg-muted font-medium leading-relaxed italic">"{review.comment}"</p>
            </div>
          ))}
        </div>

        {/* Progress + page size. The counter is `allReviews.length` of `count`,
            which is genuinely correct here -- unlike the other pages, the user has
            actually seen every row up to this point, so it is not the "this page's
            row count" bug. */}
        {allReviews.length > 0 && (
          <div className="mt-8 flex flex-col sm:flex-row items-center justify-between gap-3">
            <p className="text-xs text-fg-subtle" aria-live="polite">
              Showing <span className="font-semibold text-fg-muted">{allReviews.length}</span> of{" "}
              <span className="font-semibold text-fg-muted">{data?.count ?? 0}</span> reviews
            </p>
            <label className="flex items-center gap-2 text-xs text-fg-subtle">
              <span>Per page</span>
              <select
                value={pageSize}
                onChange={(e) => {
                  // A different page size invalidates what has been loaded, so
                  // start over from page 1 rather than stitching mismatched pages.
                  setAllReviews([]);
                  setLoadedThrough(0);
                  setPage(1);
                  setPageSize(Number(e.target.value));
                }}
                aria-label="Reviews per page"
                className="h-9 rounded-lg border border-line bg-surface px-2 text-xs font-semibold text-fg-muted focus-visible:outline-none focus-visible:border-brand"
              >
                {PAGE_SIZE_OPTIONS.map((n) => (
                  <option key={n} value={n}>{n}</option>
                ))}
              </select>
            </label>
          </div>
        )}

        {/* Was gated on `data?.next`, which was undefined on the old bare-array
            response -- so this button never appeared and only the first page of
            reviews was ever reachable. */}
        {data?.next && (
          <div className="mt-6 text-center">
            <button
              type="button"
              onClick={() => setPage(page + 1)}
              disabled={isFetching}
              className="px-8 py-4 bg-surface border-2 border-primary text-brand rounded-2xl font-black text-xs uppercase tracking-[0.2em] hover:bg-brand-hover hover:text-brand-fg transition-all active:scale-95 disabled:opacity-50"
            >
              {isFetching ? "Loading..." : "Load More Reviews"}
            </button>
          </div>
        )}

        {allReviews.length === 0 && (
          <div className="text-center py-20 bg-surface rounded-3xl border border-dashed border-line-strong">
            <MessageSquare className="mx-auto text-fg-subtle mb-4" size={48} />
            <p className="text-fg-subtle font-black uppercase tracking-widest text-xs">No reviews found</p>
          </div>
        )}
      </div>
    </div>
  );
}