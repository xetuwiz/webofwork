import React from "react";
import { useParams, Link } from "react-router-dom";
import { Star, Clock, Truck, Check, Calendar, LayoutGrid } from "lucide-react";
import { useClientFreelancerProfile } from "./freelancerProfileQueries";
import { useModal } from "../../../hooks/modal/useModalStore";
import { formatDateDMY } from "../../../utils/date";

function PackageOption({ title, price, features, isPopular = false }) {
  return (
    <div className={`relative flex flex-col p-8 rounded-2xl border transition-all duration-slow ${isPopular ? "border-primary ring-1 ring-primary bg-surface" : "border-line bg-surface hover:border-line-strong"}`}>
      {isPopular && (
        <div className="absolute -top-3 right-8 px-4 py-1 rounded-full bg-brand text-[10px] font-black text-brand-fg uppercase tracking-widest shadow-lg shadow-blue-200">
          Popular
        </div>
      )}
      <h4 className="text-sm font-black text-fg-subtle uppercase tracking-widest mb-1">{title}</h4>
      <div className="flex items-baseline gap-1 mt-2 mb-8">
        <span className="text-3xl font-black text-fg">${price}</span>
        <span className="text-fg-subtle text-xs font-bold uppercase tracking-tight">/ Project</span>
      </div>
      <div className="space-y-4 flex-1">
        {features.map((feature, index) => (
          <div key={index} className="flex items-start gap-3">
            <div className="mt-0.5 flex-shrink-0 w-5 h-5 rounded-full bg-brand-soft flex items-center justify-center">
              <Check className="w-3 h-3 text-brand" strokeWidth={3} />
            </div>
            <span className="text-sm text-fg-muted font-medium leading-tight">{feature}</span>
          </div>
        ))}
      </div>
    </div>
  );
}

export default function FreelancerProfile() {
  const { id } = useParams();
  const { data: freelancer, isLoading, isError } = useClientFreelancerProfile(id);
  const { openModal } = useModal();

  const handleInviteClick = () => {
    const packageArray = freelancer?.packages 
      ? Object.entries(freelancer.packages).map(([type, details]) => ({
          ...details,
          package_type: type,
          id: details.id || type 
        }))
      : [];
    openModal('job-invitation', {
      freelancerId: id,
      packages: packageArray
    });
  };

  if (isLoading) return (
    <div className="min-h-screen bg-surface-2 flex items-center justify-center">
      <div className="flex flex-col items-center gap-4">
        <div className="animate-spin rounded-full h-12 w-12 border-4 border-blue-600 border-t-transparent" />
        <p className="text-xs font-black uppercase tracking-widest text-fg-subtle">Loading Profile</p>
      </div>
    </div>
  );

  if (isError || !freelancer) return (
    <div className="min-h-screen bg-surface-2 flex items-center justify-center text-fg-subtle font-bold">
      Profile Not Found
    </div>
  );

  return (
    <div className="min-h-screen bg-surface-2 pb-20 selection:bg-brand-soft">
      <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-8 lg:py-16">
        <h1 className="text-3xl lg:text-4xl font-black text-fg mb-10 tracking-tight">About This Freelancer</h1>
        
        <div className="grid grid-cols-1 lg:grid-cols-[320px_1fr] gap-8 xl:gap-12 items-start">
          <aside className="lg:sticky lg:top-20 space-y-6">
            <div className="bg-surface rounded-2xl border border-line p-8 text-center shadow-sm">
              <div className="relative inline-block">
                {freelancer?.profile_photo ? (
                  <img src={freelancer.profile_photo} className="w-32 h-32 rounded-2xl mx-auto object-cover border-4 border-white shadow-xl" alt="" />
                ) : (
                  <div className="w-32 h-32 bg-brand text-brand-fg font-black flex justify-center items-center text-4xl rounded-3xl mx-auto shadow-xl">
                    {freelancer.full_name[0]}
                  </div>
                )}
                <div className="absolute -bottom-2 -right-2 w-8 h-8 bg-success-soft border-4 border-white rounded-full shadow-lg"></div>
              </div>

              <h2 className="mt-6 text-xl font-black text-fg tracking-tight">{freelancer.full_name}</h2>
              <p className="text-fg-subtle text-sm font-bold mt-1 uppercase tracking-tighter">{freelancer.tagline}</p>
              
              <div className="flex items-center justify-center gap-1.5 mt-3 mb-8">
                <Star className="w-4 h-4 fill-yellow-400 text-warning" />
                <span className="text-sm font-black text-fg">{freelancer.average_rating}</span>
                <span className="text-xs text-fg-subtle font-bold uppercase tracking-tight">({freelancer.review_count} Reviews)</span>
              </div>

              <button 
                onClick={handleInviteClick} 
                className="w-full py-4 bg-brand text-brand-fg rounded-xl font-black text-xs uppercase tracking-widest hover:bg-brand-hover transition-all shadow-xl shadow-blue-100 active:scale-[0.98]"
              >
                Invite to job
              </button>

              <div className="mt-8 space-y-6 text-left border-t border-line-soft pt-8">
                <div className="flex items-start gap-4 text-fg-subtle">
                  <LayoutGrid className="w-4 h-4 mt-0.5" />
                  <div>
                    <div className="text-[10px] font-black uppercase tracking-widest">Category</div>
                    <span className="block text-sm text-fg font-bold mt-0.5">{freelancer.category_name || 'General'}</span>
                  </div>
                </div>
                <div className="flex items-start gap-4 text-fg-subtle">
                  <Calendar className="w-4 h-4 mt-0.5" />
                  <div>
                    <div className="text-[10px] font-black uppercase tracking-widest">Member Since</div>
                    <span className="block text-sm text-fg font-bold mt-0.5">
                      {freelancer.created_at ? formatDateDMY(freelancer.created_at) : 'June 2021'}
                    </span>
                  </div>
                </div>
                <div className="flex items-start gap-4 text-fg-subtle">
                  <Clock className="w-4 h-4 mt-0.5" />
                  <div>
                    <div className="text-[10px] font-black uppercase tracking-widest">Availability</div>
                    <span className="block text-sm text-success font-black mt-0.5">
                      {freelancer.availability ? 'OPEN FOR PROJECTS' : 'UNAVAILABLE'}
                    </span>
                  </div>
                </div>
                <div className="flex items-start gap-4 text-fg-subtle">
                  <Truck className="w-4 h-4 mt-0.5" />
                  <div>
                    <div className="text-[10px] font-black uppercase tracking-widest">Completed Projects</div>
                    <span className="block text-sm text-fg font-bold mt-0.5">{freelancer.completed_projects_count} Records</span>
                  </div>
                </div>
              </div>
            </div>

            <div className="bg-surface rounded-2xl border border-line p-8 shadow-sm">
              <h3 className="text-[10px] font-black uppercase tracking-[0.2em] text-fg-subtle mb-6">Expertise & Skills</h3>
              <div className="flex flex-wrap gap-2">
                {freelancer.skills?.map((skill, i) => (
                  <span key={i} className="px-3 py-1.5 bg-surface-2 text-fg-muted text-[11px] font-black rounded-xl border border-line uppercase tracking-tighter transition-colors hover:bg-surface-3">
                    {skill}
                  </span>
                ))}
              </div>
            </div>
          </aside>

          <div className="space-y-8 min-w-0">
            <section className="bg-surface rounded-2xl p-8 lg:p-12 shadow-sm border border-line">
              <h3 className="text-lg font-black text-fg mb-6 uppercase tracking-tight">About Me</h3>
              <p className="text-fg-muted leading-relaxed text-sm lg:text-base font-medium whitespace-pre-wrap">{freelancer.bio}</p>
            </section>

            <section className="bg-surface rounded-2xl p-8 lg:p-12 shadow-sm border border-line">
              <h3 className="text-lg font-black text-fg mb-10 uppercase tracking-tight">Portfolio</h3>
              <div className="grid grid-cols-1 sm:grid-cols-2 gap-8">
                {freelancer.portfolios?.map((item, i) => (
                  <div key={i} className="group cursor-pointer">
                    <div className="aspect-[16/10] rounded-2xl overflow-hidden bg-surface-2 border border-line-soft shadow-sm">
                      <img src={item.image_url} className="w-full h-full object-cover group-hover:scale-105 transition-transform duration-deliberate ease-standard" alt="" />
                    </div>
                    <div className="mt-5 px-1">
                      <h4 className="font-black text-fg tracking-tight group-hover:text-brand transition-colors">{item.title}</h4>
                      <p className="text-xs text-fg-subtle font-bold uppercase tracking-tight mt-1 overflow-hidden">{item.description}</p>
                    </div>
                  </div>
                ))}
              </div>
            </section>

            <section className="bg-surface rounded-2xl p-8 lg:p-12 shadow-sm border border-line">
              <h3 className="text-lg font-black text-fg mb-10 uppercase tracking-tight">Select a Package</h3>
              <div className="grid grid-cols-1 md:grid-cols-2 gap-8">
                {freelancer.packages && Object.entries(freelancer.packages).map(([type, pkg]) => (
                  <PackageOption
                    key={type}
                    title={`${type} Package`}
                    price={pkg.price}
                    isPopular={type.toLowerCase() === "pro" || type.toLowerCase() === "standard"}
                    features={[
                      ...(Array.isArray(pkg.description) ? pkg.description : [pkg.description]),
                      `${pkg.delivery_days} Days Express Delivery`
                    ]}
                  />
                ))}
              </div>
            </section>

            <section className="bg-surface rounded-2xl p-8 lg:p-12 shadow-sm border border-line">
              <div className="flex justify-between items-center mb-10">
                <h3 className="text-lg font-black text-fg uppercase tracking-tight">Client Reviews</h3>
                {freelancer.review_count > 10 && (
                  <Link 
                    to={`reviews`}
                    className="text-[10px] font-black uppercase tracking-widest text-brand hover:text-brand transition-colors"
                  >
                    See All {freelancer.review_count} Reviews
                  </Link>
                )}
              </div>
              <div className="space-y-6">
                {freelancer.reviews?.length > 0 ? (
                  freelancer.reviews.map((review, i) => (
                    <div key={i} className="pb-8 border-b border-line-soft last:border-0 last:pb-0">
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
                  ))
                ) : (
                  <p className="text-fg-subtle font-bold text-sm uppercase tracking-widest py-4">No reviews yet</p>
                )}
              </div>
            </section>

            <div className="rounded-2xl p-10 lg:p-16 flex flex-col md:flex-row items-center justify-between gap-8 text-center md:text-left shadow-2xl border border-line-strong">
              <div className="max-w-md">
                <h3 className="text-2xl lg:text-3xl font-black text-fg mb-3 tracking-tight">Ready to start your project?</h3>
                <p className="text-fg text-sm font-medium leading-relaxed uppercase tracking-widest text-[10px]">Strategic collaboration for exceptional results. Initiate contact for priority scheduling.</p>
              </div>
              <button 
                onClick={handleInviteClick} 
                className="whitespace-nowrap px-10 py-5 bg-brand text-brand-fg rounded-2xl font-black text-xs uppercase tracking-[0.2em] hover:bg-brand-hover transition-all shadow-xl shadow-blue-900/40 active:scale-[0.98]"
              >
                Invite to Job
              </button>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
}
