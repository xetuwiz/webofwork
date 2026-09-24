import { Link } from "react-router-dom";
import { useAllJobs } from "./findJobsQueries.js";

export default function FindJobs() {
  const { data, isLoading, isError } = useAllJobs();
  const jobs = data?.results || data || [];

  if (isLoading) return <div className="p-8 text-center text-slate-500">Loading opportunities…</div>;
  if (isError) return <div className="p-8 text-center text-red-600">Failed to load opportunities.</div>;

  return (
    <div className="mx-auto max-w-6xl px-4 py-8 sm:px-6 lg:px-8">
      <div className="mb-8">
        <p className="text-sm font-semibold uppercase tracking-[0.22em] text-blue-600">Freelancer</p>
        <h1 className="mt-2 text-3xl font-bold text-slate-900">Find work</h1>
      </div>

      {jobs.length === 0 ? (
        <div className="rounded-2xl border border-dashed border-slate-200 bg-slate-50 p-12 text-center text-slate-500">No opportunities available right now.</div>
      ) : (
        <div className="grid gap-5 lg:grid-cols-2">
          {jobs.map((job) => (
            <article key={job.id ?? job.Id} className="rounded-2xl border border-slate-200 bg-white p-5 shadow-sm">
              <div className="flex items-start justify-between gap-4">
                <div>
                  <h2 className="text-xl font-semibold text-slate-900">{job.title ?? job.Title}</h2>
                  <p className="mt-2 text-sm text-slate-500">{job.categoryName ?? job.category ?? "General"}</p>
                </div>
                <span className="rounded-full bg-emerald-50 px-2.5 py-1 text-xs font-semibold text-emerald-700">${Number(job.fixedPrice ?? job.minPrice ?? job.maxPrice ?? 0).toLocaleString()}</span>
              </div>
              <p className="mt-4 text-sm text-slate-600 line-clamp-3">{job.description ?? job.summary ?? "No summary provided."}</p>
              <div className="mt-5 flex items-center justify-between">
                <span className="text-xs font-medium uppercase tracking-wide text-slate-500">{job.status ?? "Open"}</span>
                <Link to={`/jobs/${job.id ?? job.Id}`} className="rounded-xl bg-blue-600 px-3.5 py-2 text-sm font-semibold text-white hover:bg-blue-700">View details</Link>
              </div>
            </article>
          ))}
        </div>
      )}
    </div>
  );
}
