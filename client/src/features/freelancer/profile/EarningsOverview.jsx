import { useFreelancerTransactions } from "./profileQueries.js";

export default function EarningsOverview() {
  const { data: transactions = [], isLoading } = useFreelancerTransactions();

  const rows = Array.isArray(transactions?.results) ? transactions.results : Array.isArray(transactions) ? transactions : [];

  const totalEarned = rows.reduce((sum, item) => sum + Number(item.amount ?? item.total ?? 0), 0);
  const pending = rows.filter((item) => String(item.status ?? "").toLowerCase() === "pending").reduce((sum, item) => sum + Number(item.amount ?? 0), 0);
  const paid = rows.filter((item) => ["paid", "completed", "approved"].includes(String(item.status ?? "").toLowerCase())).reduce((sum, item) => sum + Number(item.amount ?? 0), 0);

  if (isLoading) return <div className="p-8 text-sm text-slate-500">Loading earnings...</div>;

  return (
    <div className="mx-auto max-w-5xl space-y-6 px-4 py-10">
      <div>
        <h1 className="text-3xl font-bold text-slate-900">Earnings overview</h1>
        <p className="text-sm text-slate-500">Track the flow of paid and pending client settlements.</p>
      </div>

      <div className="grid gap-4 md:grid-cols-3">
        <div className="rounded-2xl border border-slate-200 bg-white p-5 shadow-sm">
          <p className="text-xs font-semibold uppercase tracking-[0.15em] text-slate-500">Total earned</p>
          <p className="mt-3 text-3xl font-bold text-slate-900">${totalEarned.toFixed(2)}</p>
        </div>
        <div className="rounded-2xl border border-slate-200 bg-white p-5 shadow-sm">
          <p className="text-xs font-semibold uppercase tracking-[0.15em] text-slate-500">Pending</p>
          <p className="mt-3 text-3xl font-bold text-slate-900">${pending.toFixed(2)}</p>
        </div>
        <div className="rounded-2xl border border-slate-200 bg-white p-5 shadow-sm">
          <p className="text-xs font-semibold uppercase tracking-[0.15em] text-slate-500">Paid</p>
          <p className="mt-3 text-3xl font-bold text-slate-900">${paid.toFixed(2)}</p>
        </div>
      </div>

      <div className="rounded-2xl border border-slate-200 bg-white p-5 shadow-sm">
        <h2 className="mb-4 text-lg font-bold text-slate-900">Transaction history</h2>
        <div className="overflow-hidden rounded-xl border border-slate-200">
          <table className="min-w-full divide-y divide-slate-200 text-left text-sm text-slate-700">
            <thead className="bg-slate-50">
              <tr>
                <th className="px-4 py-3 font-semibold">Date</th>
                <th className="px-4 py-3 font-semibold">Status</th>
                <th className="px-4 py-3 font-semibold">Amount</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-200 bg-white">
              {rows.length > 0 ? (
                rows.map((row, index) => (
                  <tr key={`${row.id || row.reference || index}`}>
                    <td className="px-4 py-3">{row.date || row.created_at || "—"}</td>
                    <td className="px-4 py-3 capitalize">{row.status || "pending"}</td>
                    <td className="px-4 py-3 font-medium">${Number(row.amount ?? row.total ?? 0).toFixed(2)}</td>
                  </tr>
                ))
              ) : (
                <tr>
                  <td colSpan="3" className="px-4 py-6 text-center text-slate-400">No transactions yet.</td>
                </tr>
              )}
            </tbody>
          </table>
        </div>
      </div>
    </div>
  );
}
