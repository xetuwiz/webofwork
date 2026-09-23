import { Link, useParams } from "react-router-dom";
import { ArrowRight, Briefcase, MapPin, Star } from "lucide-react";
import { useClientFreelancerProfile } from "./freelancerProfileQueries.js";

export default function FreelancerProfile() {
  const { id } = useParams();
  const { data: profile, isLoading, isError, error } = useClientFreelancerProfile(id);

  if (isLoading) {
    return <div className="p-8 text-sm text-slate-500">Loading freelancer profile...</div>;
  }

  if (isError || !profile) {
    return <div className="p-8 text-sm text-red-500">{error?.response?.data?.detail || "Unable to load freelancer profile."}</div>;
  }

  return (
    <div className="mx-auto max-w-6xl space-y-6 px-4 py-10">
      <div className="rounded-2xl border border-slate-200 bg-white p-6 shadow-sm">
        <div className="flex flex-col gap-6 md:flex-row md:items-center md:justify-between">
          <div className="flex items-center gap-4">
            <div className="flex h-16 w-16 items-center justify-center rounded-full bg-blue-600 text-xl font-bold text-white">
              {profile.full_name?.[0] ?? "F"}
            </div>
            <div>
              <h1 className="text-3xl font-bold text-slate-900">{profile.full_name}</h1>
              <p className="text-sm text-blue-600">{profile.tagline}</p>
            </div>
          </div>

          <div className="flex items-center gap-3 text-sm text-slate-600">
            <span className="inline-flex items-center gap-1 rounded-full bg-yellow-50 px-3 py-1 font-medium text-yellow-700">
              <Star className="h-4 w-4 fill-yellow-400 text-yellow-400" /> {profile.average_rating ?? 0}
            </span>
            <span>{profile.review_count ?? 0} reviews</span>
          </div>
        </div>
      </div>

      <div className="grid gap-6 lg:grid-cols-[1.3fr_0.7fr]">
        <div className="space-y-6">
          <div className="rounded-2xl border border-slate-200 bg-white p-6 shadow-sm">
            <h2 className="mb-3 text-lg font-bold text-slate-900">About</h2>
            <p className="text-sm leading-7 text-slate-600">{profile.bio || "This freelancer has not added a bio yet."}</p>
          </div>

          <div className="rounded-2xl border border-slate-200 bg-white p-6 shadow-sm">
            <div className="mb-4 flex items-center justify-between">
              <h2 className="text-lg font-bold text-slate-900">Skills</h2>
              <Link to={`/find-talents/${id}/reviews`} className="inline-flex items-center gap-2 text-sm font-medium text-blue-600 hover:text-blue-700">
                View reviews <ArrowRight className="h-4 w-4" />
              </Link>
            </div>

            <div className="flex flex-wrap gap-2">
              {(profile.skills ?? []).map((skill, index) => (
                <span key={`${skill}-${index}`} className="rounded-full border border-blue-100 bg-blue-50 px-3 py-1.5 text-xs font-medium text-blue-700">
                  {skill}
                </span>
              ))}
            </div>
          </div>

          <div className="rounded-2xl border border-slate-200 bg-white p-6 shadow-sm">
            <h2 className="mb-4 text-lg font-bold text-slate-900">Packages</h2>
            <div className="grid gap-4 md:grid-cols-2">
              {profile.packages?.starter && (
                <div className="rounded-xl border border-slate-200 bg-slate-50 p-4">
                  <p className="text-sm font-semibold text-slate-800">Starter</p>
                  <p className="mt-2 text-2xl font-bold text-slate-900">${profile.packages.starter.price ?? 0}</p>
                  <p className="mt-2 text-xs text-slate-500">{profile.packages.starter.delivery_days ?? 0} day delivery</p>
                  <ul className="mt-4 space-y-2 text-sm text-slate-600">
                    {(Array.isArray(profile.packages.starter.description) ? profile.packages.starter.description : (profile.packages.starter.description || "").split("\n")).filter(Boolean).map((line, index) => (
                      <li key={`${line}-${index}`}>• {line}</li>
                    ))}
                  </ul>
                </div>
              )}

              {profile.packages?.pro && (
                <div className="rounded-xl border border-slate-200 bg-slate-50 p-4">
                  <p className="text-sm font-semibold text-slate-800">Pro</p>
                  <p className="mt-2 text-2xl font-bold text-slate-900">${profile.packages.pro.price ?? 0}</p>
                  <p className="mt-2 text-xs text-slate-500">{profile.packages.pro.delivery_days ?? 0} day delivery</p>
                  <ul className="mt-4 space-y-2 text-sm text-slate-600">
                    {(Array.isArray(profile.packages.pro.description) ? profile.packages.pro.description : (profile.packages.pro.description || "").split("\n")).filter(Boolean).map((line, index) => (
                      <li key={`${line}-${index}`}>• {line}</li>
                    ))}
                  </ul>
                </div>
              )}
            </div>
          </div>
        </div>

        <div className="space-y-6">
          <div className="rounded-2xl border border-slate-200 bg-white p-6 shadow-sm">
            <h2 className="text-lg font-bold text-slate-900">Profile details</h2>
            <ul className="mt-4 space-y-3 text-sm text-slate-600">
              <li className="flex items-center gap-2"><Briefcase className="h-4 w-4 text-blue-600" /> {profile.category || "Category not set"}</li>
              <li className="flex items-center gap-2"><MapPin className="h-4 w-4 text-blue-600" /> {profile.location || "Location not shared"}</li>
            </ul>
          </div>

          <div className="rounded-2xl border border-slate-200 bg-white p-6 shadow-sm">
            <h2 className="mb-4 text-lg font-bold text-slate-900">Portfolio</h2>
            <div className="space-y-4">
              {(profile.portfolios ?? []).map((item) => (
                <div key={item.id} className="overflow-hidden rounded-xl border border-slate-200 bg-slate-50">
                  {item.image_url ? <img src={item.image_url} alt={item.title} className="h-32 w-full object-cover" /> : null}
                  <div className="p-4">
                    <h3 className="text-sm font-semibold text-slate-800">{item.title}</h3>
                    <p className="mt-2 text-xs leading-6 text-slate-600">{item.description}</p>
                  </div>
                </div>
              ))}
            </div>
          </div>
        </div>
      </div>
    </div>
  );
}
