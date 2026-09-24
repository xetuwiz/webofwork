import { useState } from "react";
import { Link } from "react-router-dom";
import InvitationModal from "./InvitationModal.jsx";

export default function FreelancerCard({ freelancer }) {
  const [open, setOpen] = useState(false);
  const profile = freelancer || {};
  const name = profile.fullName || profile.full_name || profile.name || "Freelancer";
  const headline = profile.tagline || profile.title || "Independent contractor";
  const rating = Number(profile.rating ?? profile.averageRating ?? 0);
  const skills = profile.skills || profile.topSkills || [];

  return (
    <>
      <article className="rounded-2xl border border-slate-200 bg-white p-5 shadow-sm">
        <div className="flex items-center justify-between gap-3">
          <div>
            <h2 className="text-lg font-semibold text-slate-900">{name}</h2>
            <p className="text-sm text-slate-500">{headline}</p>
          </div>
          <div className="rounded-full bg-amber-50 px-2.5 py-1 text-xs font-semibold text-amber-700">★ {rating.toFixed(1)}</div>
        </div>

        <div className="mt-4 flex flex-wrap gap-2">
          {skills.slice(0, 4).map((skill, index) => (
            <span key={`${skill?.name ?? skill ?? index}`} className="rounded-full border border-slate-200 bg-slate-50 px-2.5 py-1 text-[11px] font-medium text-slate-700">
              {skill?.name ?? skill}
            </span>
          ))}
        </div>

        <div className="mt-5 flex items-center justify-between gap-3">
          <Link to={`/find-talents/${profile.id ?? profile.Id}`} className="text-sm font-semibold text-blue-600 hover:text-blue-700">View profile</Link>
          <button type="button" onClick={() => setOpen(true)} className="rounded-xl bg-blue-600 px-3.5 py-2 text-sm font-semibold text-white hover:bg-blue-700">Invite</button>
        </div>
      </article>

      <InvitationModal freelancer={profile} isOpen={open} onClose={() => setOpen(false)} />
    </>
  );
}
