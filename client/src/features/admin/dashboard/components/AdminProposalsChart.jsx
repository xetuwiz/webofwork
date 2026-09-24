import { useProposalsChart } from "../dashboardQueries.js";

export default function AdminProposalsChart() {
  const query = useProposalsChart({ days: 30 });
  if (query.isLoading) return <div className="admin-loading">Loading proposals chart…</div>;
  if (query.isError) return <div className="admin-form-error">Proposals chart unavailable.</div>;
  return <div className="admin-chart-list">{(query.data?.items ?? query.data ?? []).map((row) => <div key={row.date}><span>{row.date}</span><strong>{row.submitted} submitted</strong><small>{row.accepted} accepted · {row.rejected} rejected</small></div>)}</div>;
}
