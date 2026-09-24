import { useRevenueChart } from "../dashboardQueries.js";
import formatMoney from "../../../../utils/formatMoney.js";

export default function AdminRevenueChart() {
  const query = useRevenueChart({ days: 30 });
  if (query.isLoading) return <div className="admin-loading">Loading revenue chart…</div>;
  if (query.isError) return <div className="admin-form-error">Revenue chart unavailable.</div>;
  return <div className="admin-chart-list">{(query.data?.items ?? query.data ?? []).map((row) => <div key={row.date}><span>{row.date}</span><strong>{formatMoney(row.revenueUsd)}</strong></div>)}</div>;
}
