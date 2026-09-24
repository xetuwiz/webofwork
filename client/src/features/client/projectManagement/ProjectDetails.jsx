import { Link, useNavigate, useParams } from "react-router-dom";
import { useProjectDetails } from "./projectQueries.js";
import { useDeleteProject } from "./projectMutations.js";
import DeleteProjectModal from "./components/DeleteProjectModal.jsx";
import { useState } from "react";

export default function ProjectDetails() {
  const { id } = useParams();
  const navigate = useNavigate();
  const { data, isLoading, isError } = useProjectDetails(id);
  const mutation = useDeleteProject();
  const [confirming, setConfirming] = useState(false);

  if (isLoading) return <div className="p-8 text-center text-slate-500">Loading project…</div>;
  if (isError || !data) return <div className="p-8 text-center text-red-600">Project not found.</div>;

  const budget = data.fixedPrice ?? data.minPrice ?? data.maxPrice ?? 0;
  const skills = data.skills || [];

  return (
    <div className="mx-auto max-w-5xl px-4 py-8 sm:px-6 lg:px-8">
      <div className="mb-6 flex flex-wrap items-center justify-between gap-3">
        <div>
          <p className="text-sm font-semibold uppercase tracking-[0.22em] text-blue-600">Project</p>
          <h1 className="mt-2 text-3xl font-bold text-slate-900">{data.title}</h1>
        </div>
        <div className="flex gap-3">
          <Link to={`/my-projects/${id}/edit`} className="rounded-xl border border-slate-200 px-4 py-2.5 text-sm font-medium text-slate-700 hover:bg-slate-50">Edit</Link>
          <Link to={`/my-projects/${id}/proposals`} className="rounded-xl bg-blue-600 px-4 py-2.5 text-sm font-semibold text-white hover:bg-blue-700">View proposals</Link>
          <button type="button" onClick={() => setConfirming(true)} className="rounded-xl border border-red-200 bg-red-50 px-4 py-2.5 text-sm font-semibold text-red-700 hover:bg-red-100">Archive</button>
        </div>
      </div>

      <div className="grid gap-6 lg:grid-cols-[2fr,1fr]">
        <div className="space-y-6 rounded-2xl border border-slate-200 bg-white p-6 shadow-sm">
          <div>
            <h2 className="text-sm font-semibold uppercase tracking-[0.2em] text-slate-500">Overview</h2>
            <p className="mt-3 text-slate-700">{data.description}</p>
          </div>

          <div className="grid gap-4 sm:grid-cols-3">
            <div className="rounded-xl bg-slate-50 p-4"><p className="text-xs uppercase tracking-wide text-slate-500">Budget</p><p className="mt-2 text-lg font-bold text-slate-900">${Number(budget || 0).toLocaleString()}</p></div>
            <div className="rounded-xl bg-slate-50 p-4"><p className="text-xs uppercase tracking-wide text-slate-500">Delivery</p><p className="mt-2 text-lg font-bold text-slate-900">{data.expectedDays || 0} days</p></div>
            <div className="rounded-xl bg-slate-50 p-4"><p className="text-xs uppercase tracking-wide text-slate-500">Status</p><p className="mt-2 text-lg font-bold text-slate-900">{data.status || "Open"}</p></div>
          </div>

          <div>
            <h3 className="text-lg font-semibold text-slate-900">Requirements</h3>
            <p className="mt-2 whitespace-pre-line text-slate-700">{data.requirements || "No requirements specified."}</p>
          </div>

          <div>
            <h3 className="text-lg font-semibold text-slate-900">Deliverables</h3>
            <p className="mt-2 whitespace-pre-line text-slate-700">{data.expectedDeliverables || "No deliverables specified."}</p>
          </div>

          <div>
            <h3 className="text-lg font-semibold text-slate-900">Skills</h3>
            <div className="mt-3 flex flex-wrap gap-2">
              {skills.length > 0 ? skills.map((skill) => (
                <span key={skill.skillId ?? skill.id ?? skill.name ?? skill.Name} className="rounded-full border border-slate-200 bg-slate-50 px-3 py-1.5 text-xs font-medium text-slate-700">
                  {skill.name ?? skill.Name ?? skill.skillName}
                </span>
              )) : <span className="text-sm text-slate-500">No skills selected.</span>}
            </div>
          </div>
        </div>

        <aside className="space-y-6 rounded-2xl border border-slate-200 bg-white p-6 shadow-sm">
          <div>
            <h2 className="text-sm font-semibold uppercase tracking-[0.2em] text-slate-500">Quick actions</h2>
            <div className="mt-4 space-y-3">
              <button type="button" onClick={() => navigate(`/my-projects/${id}/proposals`)} className="w-full rounded-xl border border-slate-200 px-4 py-3 text-left text-sm font-medium text-slate-700 hover:bg-slate-50">Review proposals</button>
              <button type="button" onClick={() => navigate("/find-talents")} className="w-full rounded-xl border border-slate-200 px-4 py-3 text-left text-sm font-medium text-slate-700 hover:bg-slate-50">Find talent</button>
            </div>
          </div>
        </aside>
      </div>

      <DeleteProjectModal
        project={data}
        pending={mutation.isPending}
        onClose={() => setConfirming(false)}
        isOpen={confirming}
        onConfirm={() => {
          mutation.mutate({ id: Number(id) }, { onSuccess: () => { setConfirming(false); navigate("/my-projects"); } });
        }}
      />
    </div>
  );
}
