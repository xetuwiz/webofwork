import React from "react";
import { Star, Calendar, Briefcase, Clock, Check, Edit3, Eye, CreditCard, Bell } from "lucide-react";
import { useFreelancerProfile } from "./profileQueries";
import { getErrorMessage } from "../../../utils/errorMessage";
import { formatDateDMY } from "../../../utils/date";
import { Link } from "react-router-dom";

export default function Profile() {
  const { data: profile, isLoading, isError, error } = useFreelancerProfile();

  if (isLoading) {
    return (
      <div className="min-h-screen flex items-center justify-center">
        <div className="animate-spin rounded-full h-12 w-12 border-b-2 border-blue-600"></div>
      </div>
    );
  }

  if (isError) {
    return (
      <div className="min-h-screen flex items-center justify-center text-danger-soft-fg">
        Error: {getErrorMessage(error, "Failed to load profile")}
      </div>
    );
  }

  if (!profile) return null;

  const date = formatDateDMY(profile.created_at);

  const renderDescription = (desc) => {
    if (!desc) return null;
    const features = Array.isArray(desc) ? desc : desc.split("\n").filter(line => line.trim() !== "");
    return features.map((feature, i) => (
      <li key={i} className="flex items-center gap-2 text-sm text-fg-muted">
        <Check size={14} className="text-success-soft-fg font-bold" /> {feature}
      </li>
    ));
  };

  return (
    <div className="min-h-screen bg-surface-2 py-8 px-4 sm:px-6 lg:px-8">
      <div className="max-w-5xl mx-auto space-y-6">
        <div className="flex flex-col md:flex-row md:items-center justify-between gap-4">
          <div>
            <h1 className="text-2xl font-bold text-fg">My Profile</h1>
            <p className="text-fg-subtle text-sm">View and manage your freelance professional profile.</p>
          </div>

          <div className="flex flex-col sm:flex-row gap-2 sm:gap-3 w-full sm:w-auto">
  <Link
    to={'/freelancer/settings/payment'}
    className="flex items-center justify-center gap-2 px-4 py-2.5 sm:py-2 bg-surface border border-line text-fg-muted rounded-xl sm:rounded-lg hover:bg-surface-2 font-bold sm:font-medium text-xs sm:text-sm transition-all shadow-sm"
  >
    <CreditCard size={16} /> Bank Details
  </Link>

  {/* Sits next to Bank Details rather than behind a settings menu: the two are
      both "how do you want to be paid / contacted" and burying one made it
      undiscoverable. */}
  <Link
    to={'/freelancer/settings/notifications'}
    className="flex items-center justify-center gap-2 px-4 py-2.5 sm:py-2 bg-surface border border-line text-fg-muted rounded-xl sm:rounded-lg hover:bg-surface-2 font-bold sm:font-medium text-xs sm:text-sm transition-all shadow-sm"
  >
    <Bell size={16} /> Notifications
  </Link>

  <Link 
    to={'/freelancer/profile/earnings-overview'} 
    className="flex items-center justify-center gap-2 px-4 py-2.5 sm:py-2 bg-surface border border-line text-fg-muted rounded-xl sm:rounded-lg hover:bg-surface-2 font-bold sm:font-medium text-xs sm:text-sm transition-all shadow-sm"
  >
    <Eye size={16} /> View Earnings
  </Link>
  
  <Link 
    to={'edit/'} 
    className="flex items-center justify-center gap-2 px-4 py-3 sm:py-2 bg-brand text-brand-fg rounded-xl sm:rounded-lg hover:bg-brand-hover font-bold sm:font-medium text-xs sm:text-sm transition-all shadow-sm order-first sm:order-last"
  >
    <Edit3 size={16} /> Edit Profile
  </Link>
</div>
        </div>

        <div className="bg-surface rounded-xl border border-line p-6 shadow-sm">
          <div className="flex flex-col md:flex-row gap-6 items-center md:items-start">
            {profile?.profile_photo ?
              <img 
                src={profile.profile_photo}
                alt={profile.full_name} 
                className="w-24 h-24 md:w-32 md:h-32 rounded-full object-cover ring-4 ring-gray-50"
              /> :
              <span style={{ fontFamily: 'MuseoModerno, sans-serif' }} className="w-24 h-24 md:w-32 md:h-32 flex items-center justify-center text-5xl bg-brand text-brand-fg rounded-full object-cover ring-4 ring-gray-50">{profile?.full_name[0]}</span>
            }
            <div className="flex-1 text-center md:text-left">
              <div className="flex flex-col md:flex-row md:items-center justify-between gap-2 mb-4">
                <div>
                  <h2 className="text-2xl font-bold text-fg">{profile.full_name}</h2>
                  <p className="text-brand font-medium">{profile.tagline}</p>
                </div>
                <span className="inline-flex items-center px-3 py-1 rounded-full text-xs font-medium bg-success-soft text-success border border-success/30 self-center md:self-start">
                  Available
                </span>
              </div>
              
              <div className="flex flex-wrap justify-center md:justify-start gap-20 text-sm text-fg-subtle">
                <div className="flex items-center gap-1">
                  <Star className="text-warning fill-yellow-400" size={18} />
                  <span className="font-bold text-fg">{profile.average_rating}</span>
                  <span>({profile.review_count} reviews)</span>
                </div>
                <div className="flex items-center gap-1">
                  <Calendar size={16} />
                  <span className="text-fg">Member since:</span> {date}
                </div>
                <div className="flex items-center gap-1">
                  <Briefcase size={16} />
                  <span>{profile.category_name || 'General'}</span>
                </div>
              </div>
            </div>
          </div>
        </div>

        <div className="bg-surface rounded-xl border border-line p-6 shadow-sm">
          <h3 className="text-lg font-bold text-fg mb-4">Skills</h3>
          <div className="flex flex-wrap gap-2">
            {profile.skills?.map((skill, index) => (
              <span key={index} className="px-3 py-1 bg-brand-soft text-brand rounded-full text-xs font-semibold border border-brand-line">
                {skill}
              </span>
            ))}
          </div>
        </div>

        <div className="space-y-4">
          <h3 className="text-lg font-bold text-fg">Your Packages</h3>
          <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
            <div className="bg-surface rounded-xl border border-line p-6 shadow-sm hover:border-brand-line transition-colors">
              <h4 className="font-bold text-fg mb-2 text-lg">Starter Package</h4>
              <div className="flex items-baseline justify-between mb-4">
                <div className="text-2xl font-bold text-fg">${profile.packages?.starter?.price || 0}</div>
                <div className="flex items-center gap-1 text-fg-subtle text-xs">
                  <Clock size={14} /> {profile.packages?.starter?.delivery_days || 0} days delivery
                </div>
              </div>
              <ul className="space-y-3">
                {renderDescription(profile.packages?.starter?.description)}
              </ul>
            </div>

            <div className="bg-surface rounded-xl border border-line p-6 shadow-sm hover:border-brand-line transition-colors">
              <h4 className="font-bold text-fg mb-2 text-lg">Pro Package</h4>
              <div className="flex items-baseline justify-between mb-4">
                <div className="text-2xl font-bold text-fg">${profile.packages?.pro?.price || 0}</div>
                <div className="flex items-center gap-1 text-fg-subtle text-xs">
                  <Clock size={14} /> {profile.packages?.pro?.delivery_days || 0} days delivery
                </div>
              </div>
              <ul className="space-y-3">
                {renderDescription(profile.packages?.pro?.description)}
              </ul>
            </div>
          </div>
        </div>

        <div className="space-y-4">
          <h3 className="text-lg font-bold text-fg">Portfolio</h3>
          <div className="grid grid-cols-1 sm:grid-cols-2 gap-6">
            {profile.portfolios?.length > 0 ? (
              profile.portfolios.map((item) => (
                <div key={item.id} className="group bg-surface rounded-xl border border-line overflow-hidden shadow-sm">
                  <div className="relative h-48 overflow-hidden">
                    <img 
                      src={item.image_url} 
                      alt={item.title} 
                      className="w-full h-full object-cover transition-transform group-hover:scale-105" 
                    />
                  </div>
                  <div className="p-4 border-t border-line-soft">
                    <h4 className="font-bold text-fg">{item.title}</h4>
                    <p className="text-fg-subtle text-xs mt-1">{item.description}</p>
                  </div>
                </div>
              ))
            ) : (
              <p className="text-fg-subtle italic text-sm">No portfolio items added yet.</p>
            )}
          </div>
        </div>

        <div className="bg-surface rounded-xl border border-line p-8 shadow-sm">
          <h3 className="text-lg font-bold text-fg mb-4">About Me</h3>
          <div className="text-fg-muted text-sm leading-relaxed space-y-4">
            <p>{profile.bio || "No bio provided yet."}</p>
          </div>
        </div>

        <div className="space-y-4">
          <h3 className="text-lg font-bold text-fg">Client Reviews</h3>
          <div className="space-y-4">
            {profile.reviews?.length > 0 ? (
              profile.reviews.map((review) => (
                <div key={review.id} className="bg-surface rounded-xl border border-line p-6 shadow-sm">
                  <div className="flex justify-between items-start mb-3">
                    <div className="flex items-center gap-3">
                      {review.reviewer_photo ? (
                        <img src={review.reviewer_photo} alt={review.reviewer_name} className="w-10 h-10 rounded-full object-cover" />
                      ) : (
                        <div className="w-10 h-10 rounded-full bg-surface-3 flex items-center justify-center text-fg-subtle font-bold text-xs uppercase">
                          {review.reviewer_name[0]}
                        </div>
                      )}
                      <div>
                        <h4 className="font-bold text-fg text-sm">{review.reviewer_name}</h4>
                        <div className="flex gap-1 mt-1">
                          {[...Array(5)].map((_, i) => (
                            <Star 
                              key={i} 
                              size={14} 
                              className={i < review.rating ? "text-warning fill-yellow-400" : "text-fg-subtle"}
                            />
                          ))}
                        </div>
                      </div>
                    </div>
                    <div className="text-right">
                       <span className="block text-[10px] text-fg-subtle font-bold uppercase tracking-wider">{review.project_title}</span>
                       <span className="text-xs text-fg-subtle font-medium">{formatDateDMY(review.created_at)}</span>
                    </div>
                  </div>
                  <p className="text-fg-muted text-sm italic leading-relaxed">"{review.comment}"</p>
                </div>
              ))
            ) : (
              <p className="text-fg-subtle italic text-sm">No reviews received yet.</p>
            )}
          </div>
        </div>
      </div>
    </div>
  );
}