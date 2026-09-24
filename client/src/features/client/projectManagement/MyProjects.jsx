import { Link } from "react-router-dom";
import { useMyProjects } from "./projectQueries.js";
import ProjectCard from "./components/ProjectCard.jsx";

export default function MyProjects() {
  const { data, isLoading, isError } = useMyProjects();
  const projects = data?.results || data || [];

  if (isLoading) {
    return <div className="p-8 text-center text-slate-500">Loading projects…</div>;
  }

  if (isError) {
    return <div className="p-8 text-center text-red-600">Failed to load your projects.</div>;
  }

  return (
    <div className="mx-auto max-w-6xl px-4 py-8 sm:px-6 lg:px-8">
      <div className="mb-8 flex items-center justify-between gap-4">
        <div>
          <p className="text-sm font-semibold uppercase tracking-[0.22em] text-blue-600">Marketplace</p>
          <h1 className="mt-2 text-3xl font-bold text-slate-900">My projects</h1>
        </div>
        <Link to="/create-project" className="rounded-xl bg-blue-600 px-4 py-2.5 text-sm font-semibold text-white hover:bg-blue-700">Create project</Link>
      </div>

      {projects.length === 0 ? (
        <div className="rounded-2xl border border-dashed border-slate-200 bg-slate-50 p-12 text-center">
          <h2 className="text-xl font-semibold text-slate-800">No projects created yet</h2>
          <p className="mt-2 text-sm text-slate-500">Launch your first project to start receiving proposals.</p>
        </div>
      ) : (
        <div className="grid gap-5 lg:grid-cols-2">
          {projects.map((project) => (
            <ProjectCard key={project.id ?? project.Id} project={project} />
          ))}
        </div>
      )}
    </div>
  );
}
