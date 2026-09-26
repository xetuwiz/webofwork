import React from "react";
import { Calendar, Building2, MapPin, Star, MessageSquare } from "lucide-react";
import { useClientReviews, useProfile } from "./profileQueries";
import {useSelector} from 'react-redux'
import { Link } from "react-router-dom";
import Pagination from "../../../components/common/Pagination";
import { useListState } from "../../../hooks/useListState";

export default function ProfileOverview() {
  // Page and page size live in the URL, so a view of the reviews is shareable.
  const { page, pageSize, setPage, setPageSize } = useListState();

  const { data: profile, isLoading: profileLoading, isError: profileError } = useProfile();
  const user = useSelector(state=>state.auth.user)
  const { data: reviewsData, isLoading: reviewsLoading, isFetching } = useClientReviews(user?.id, { page, page_size: pageSize });
  const reviews = reviewsData?.results || [];
  const totalCount = reviewsData?.count || 0;

  if (profileLoading) return <div className="p-8 text-fg-subtle animate-pulse font-medium">Loading profile...</div>;
  if (profileError) return <div className="p-8 text-danger-soft-fg font-medium">Failed to load profile</div>;

  const { full_name, email, profile_photo, company_name, location, categories, joined_at, average_rating, review_count } = profile;
  return (
    <div className="max-w-6xl mx-auto space-y-6">
      <h1 className="text-2xl md:text-[26px] lg:mt-10 font-black text-fg mb-2">Profile Overview</h1>

      <div className="bg-surface rounded-2xl border border-line shadow-sm overflow-hidden">
        <div className="p-6 md:p-8">
          <div className="flex flex-col md:flex-row items-center md:items-start gap-6">
            <div className="relative">
              <img
                src={profile_photo || `https://ui-avatars.com/api/?name=${encodeURIComponent(full_name)}&background=3B82F6&color=fff`}
                alt="Profile"
                className="w-24 h-24 md:w-32 md:h-32 rounded-3xl object-cover shadow-lg border-4 border-white"
              />
              <div className="absolute -bottom-2 -right-2 bg-success-soft w-5 h-5 rounded-full border-4 border-white" />
            </div>
            
            <div className="flex-1 w-full text-center md:text-left">
              <div className="flex flex-col md:flex-row md:items-center justify-between gap-4 mb-6">
                <div>
                  <h2 className="text-xl md:text-2xl font-black text-fg mb-1">{full_name}</h2>
                  <div className="flex flex-wrap justify-center md:justify-start items-center gap-3">
                    <p className="text-sm text-fg-subtle font-medium">{email}</p>
                    <div className="flex items-center gap-1.5 bg-warning-soft px-2 py-1 rounded-lg">
                      <Star className="w-3.5 h-3.5 text-warning-soft-fg fill-amber-500" />
                      <span className="text-sm font-bold text-warning">{Number(average_rating || 0).toFixed(1)}</span>
                      <span className="text-xs text-warning/60">({review_count})</span>
                    </div>
                  </div>
                </div>
                <Link to="/profile/edit" className="inline-flex justify-center px-6 py-2.5 bg-brand text-brand-fg rounded-xl text-xs font-bold hover:bg-inverse transition-all active:scale-95 shadow-md">
                  Edit Profile
                </Link>
              </div>

              <div className="flex flex-col sm:flex-row flex-wrap justify-center md:justify-start items-center gap-4 text-xs font-bold text-fg-subtle uppercase tracking-widest">
                <div className="flex items-center gap-2">
                  <Calendar className="w-4 h-4" />
                  <span>Joined {joined_at ? new Date(joined_at).toLocaleDateString() : "—"}</span>
                </div>
                {company_name && (
                  <div className="flex items-center gap-2">
                    <Building2 className="w-4 h-4" />
                    <span className="text-fg-muted">{company_name}</span>
                  </div>
                )}
                {location && (
                  <div className="flex items-center gap-2">
                    <MapPin className="w-4 h-4" />
                    <span className="text-fg-muted">{location}</span>
                  </div>
                )}
              </div>
            </div>
          </div>
        </div>
      </div>

      <div className="grid grid-cols-1 gap-6">
        <section className="bg-surface rounded-2xl border border-line shadow-sm p-6 md:p-8">
          <h3 className="text-sm font-black text-fg uppercase tracking-widest mb-6 flex items-center gap-2">
            <div className="w-1 h-4 bg-brand rounded-full" />
            Skills & Interests
          </h3>
          {categories?.length > 0 ? (
            <div className="flex flex-wrap gap-2">
              {categories.map((cat) => (
                <span key={cat} className="px-4 py-2 rounded-xl border border-line-soft bg-surface-2 text-fg-muted text-xs font-bold transition-all hover:bg-surface hover:border-brand-line hover:text-brand">
                  {cat}
                </span>
              ))}
            </div>
          ) : (
            <p className="text-sm text-fg-subtle italic">No skills listed yet.</p>
          )}
        </section>

        <section className="bg-surface rounded-2xl border border-line shadow-sm p-6 md:p-8">
          <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-4 mb-8">
            <h3 className="text-sm font-black text-fg uppercase tracking-widest flex items-center gap-2">
              <div className="w-1 h-4 bg-brand rounded-full" />
              Reviews ({totalCount})
            </h3>
            {isFetching && <span className="text-[10px] bg-brand-soft text-brand px-2 py-1 rounded-md font-black animate-pulse">SYNCING</span>}
          </div>
          
          {reviewsLoading ? (
            <div className="space-y-4">
              <div className="h-24 bg-surface-2 rounded-xl shimmer" />
              <div className="h-24 bg-surface-2 rounded-xl shimmer" />
            </div>
          ) : reviews.length > 0 ? (
            <>
              <div className="space-y-6">
                {reviews.map((review) => (
                  <div key={review.id} className="p-5 bg-surface-2/50 rounded-2xl border border-line-soft transition-all hover:bg-surface hover:shadow-md">
                    <div className="flex items-center gap-4 mb-4">
                      <img 
                        src={review.reviewer_photo || `https://ui-avatars.com/api/?name=${review.reviewer_name}&background=3B82F6&color=fff`} 
                        className="w-10 h-10 rounded-xl object-cover border-2 border-white shadow-sm" 
                        alt="" 
                      />
                      <div className="flex-1">
                        <h4 className="text-sm font-black text-fg">{review.reviewer_name}</h4>
                        <div className="flex items-center gap-3">
                          <div className="flex">
                            {[...Array(5)].map((_, i) => (
                              <Star key={i} className={`w-3 h-3 ${i < review.rating ? 'text-warning fill-amber-400' : 'text-fg-subtle'}`} />
                            ))}
                          </div>
                          <span className="text-[10px] text-fg-subtle font-black uppercase tracking-widest">
                            {new Date(review.created_at).toLocaleDateString()}
                          </span>
                        </div>
                      </div>
                    </div>
                    <p className="text-sm text-fg-muted italic leading-relaxed pl-2 border-l-2 border-line">
                      "{review.comment}"
                    </p>
                  </div>
                ))}
              </div>

              {/* Was a bare prev/next with the page number and no total. The
                  arrows were driven by `reviewsData.next/previous`, which never
                  reached the query because useClientReviews had no page
                  parameter at all. */}
              <Pagination
                page={page}
                pageSize={pageSize}
                count={totalCount}
                onPageChange={setPage}
                onPageSizeChange={setPageSize}
                itemNoun="reviews"
                className="mt-8 pt-6 border-t border-line-soft"
              />
            </>
          ) : (
            <div className="text-center py-12 bg-surface-2 rounded-2xl border-2 border-dashed border-line">
              <MessageSquare className="w-10 h-10 text-fg-subtle mx-auto mb-3" />
              <p className="text-sm text-fg-subtle font-bold uppercase tracking-widest">No reviews found</p>
            </div>
          )}
        </section>
      </div>
    </div>
  );
}
