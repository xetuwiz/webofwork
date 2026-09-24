import { useParams } from "react-router-dom";
import { useJobDetails } from "./jobDetailsQueries.js";
import ProposalModal from "../proposals/components/ProposalModal.jsx";
import { useState } from "react";

export default function JobDetails() {
  const { id } = useParams();
  const { data, isLoading, isError } = useJobDetails(id);
  const [open, setOpen] = useState(false);

  if (isLoading) return <div className="p-8 text-center text-slate-500">Loading opportunity…</div>;
  if (isError || !data) return <div className="p-8 text-center text-red-600">Opportunity not found.</div>;

  const budget = Number(data.fixedPrice ?? data.minPrice ?? data.maxPrice ?? 0);

  return (
    <>
      <div className="mx-auto max-w-5xl px-4 py-8 sm:px-6 lg:px-8">
        <div className="mb-6 flex flex-wrap items-center justify-between gap-4">
          <div>
            <p className="text-sm font-semibold uppercase tracking-[0.22em] text-blue-600">Opportunity</p>
            <h1 className="mt-2 text-3xl font-bold text-slate-900">{data.title ?? data.Title}</h1>
          </div>
          <button type="button" onClick={() => setOpen(true)} className="rounded-xl bg-blue-600 px-4 py-2.5 text-sm font-semibold text-white hover:bg-blue-700">Send proposal</button>
        </div>

        <div className="grid gap-6 lg:grid-cols-[2fr,1fr]">
          <div className="space-y-6 rounded-2xl border border-slate-200 bg-white p-6 shadow-sm">
            <div>
              <h2 className="text-sm font-semibold uppercase tracking-[0.2em] text-slate-500">Description</h2>
              <p className="mt-3 text-slate-700">{data.description ?? data.summary ?? "No details provided."}</p>
            </div>

            <div className="grid gap-4 sm:grid-cols-3">
              <div className="rounded-xl bg-slate-50 p-4"><p className="text-xs uppercase tracking-wide text-slate-500">Budget</p><p className="mt-2 text-lg font-bold text-slate-900">${budget.toLocaleString()}</p></div>
              <div className="rounded-xl bg-slate-50 p-4"><p className="text-xs uppercase tracking-wide text-slate-500">Delivery</p><p className="mt-2 text-lg font-bold text-slate-900">{data.expectedDays ?? 0} days</p></div>
              <div className="rounded-xl bg-slate-50 p-4"><p className="text-xs uppercase tracking-wide text-slate-500">Category</p><p className="mt-2 text-lg font-bold text-slate-900">{data.categoryName ?? data.category ?? "General"}</p></div>
            </div>

            <div>
              <h3 className="text-lg font-semibold text-slate-900">Requirements</h3>
              <p className="mt-2 whitespace-pre-line text-slate-700">{data.requirements || "No requirements specified."}</p>
            </div>
          </div>

          <aside className="rounded-2xl border border-slate-200 bg-white p-6 shadow-sm">
            <h2 className="text-sm font-semibold uppercase tracking-[0.2em] text-slate-500">Project summary</h2>
            <div className="mt-4 space-y-3 text-sm text-slate-600">
              <div className="flex justify-between gap-3"><span>Budget</span><strong className="text-slate-900">${budget.toLocaleString()}</strong></div>
              <div className="flex justify-between gap-3"><span>Timeline</span><strong className="text-slate-900">{data.expectedDays ?? 0} days</strong></div>
              <div className="flex justify-between gap-3"><span>Status</span><strong className="text-slate-900">{data.status ?? "Open"}</strong></div>
            </div>
          </aside>
        </div>
      </div>

      <ProposalModal projectId={Number(id)} isOpen={open} onClose={() => setOpen(false)} />
    </>
  );
}
