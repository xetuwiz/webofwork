import { useParams } from "react-router-dom";
import { useProjectDetails } from "../projectQueries.js";

export default function ProposalsPage() {
  const { id } = useParams();
  const { data: project, isLoading } = useProjectDetails(id);

  if (isLoading) return <div className="p-8 text-center text-slate-500">Loading proposals…</div>;
  if (!project) return <div className="p-8 text-center text-red-600">Project not found.</div>;

  return (
    <div className="mx-auto max-w-5xl px-4 py-8 sm:px-6 lg:px-8">
      <div className="mb-6">
        <p className="text-sm font-semibold uppercase tracking-[0.22em] text-blue-600">Project</p>
        <h1 className="mt-2 text-3xl font-bold text-slate-900">{project.title}</h1>
      </div>

      <div className="rounded-2xl border border-slate-200 bg-white p-5 shadow-sm">
        <p className="text-sm text-slate-600">Proposals for this project will be listed here when the project has active submissions.</p>
      </div>
    </div>
  );
}
