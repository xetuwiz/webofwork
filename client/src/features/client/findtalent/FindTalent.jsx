import { useState } from "react";
import { useFreelancerList } from "./findTalentQueries.js";
import FreelancerCard from "./components/FreelancerCard.jsx";

export default function FindTalent() {
  const [query, setQuery] = useState("");
  const { data, isLoading, isError } = useFreelancerList({ query, limit: 20 });
  const freelancers = data?.results || data || [];

  return (
    <div className="mx-auto max-w-6xl px-4 py-8 sm:px-6 lg:px-8">
      <div className="mb-8">
        <p className="text-sm font-semibold uppercase tracking-[0.22em] text-blue-600">Marketplace</p>
        <h1 className="mt-2 text-3xl font-bold text-slate-900">Find talent</h1>
      </div>

      <div className="mb-6 rounded-2xl border border-slate-200 bg-white p-4 shadow-sm">
        <input
          value={query}
          onChange={(event) => setQuery(event.target.value)}
          placeholder="Search by name, skill, or category"
          className="w-full rounded-xl border border-slate-200 px-3 py-2.5 text-sm outline-none focus:border-blue-500 focus:ring-2 focus:ring-blue-100"
        />
      </div>

      {isLoading && <div className="text-center text-slate-500">Loading freelancers…</div>}
      {isError && <div className="text-center text-red-600">Failed to load freelancers.</div>}

      {!isLoading && !isError && (
        <div className="grid gap-5 md:grid-cols-2 xl:grid-cols-3">
          {freelancers.length > 0 ? freelancers.map((person) => <FreelancerCard key={person.id ?? person.Id} freelancer={person} />) : <div className="md:col-span-2 xl:col-span-3 rounded-2xl border border-dashed border-slate-200 bg-slate-50 p-12 text-center text-slate-500">No freelancers match your search.</div>}
        </div>
      )}
    </div>
  );
}
