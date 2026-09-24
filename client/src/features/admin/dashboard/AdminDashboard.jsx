import { useState } from "react";
import { useDashboardStats, useRevenueChart, useProposalsChart } from "./dashboardQueries.js";
import AdminStatsCard from "./components/AdminStatsCard.jsx";
import formatMoney from "../../../utils/formatMoney.js";

export default function AdminDashboard() {
  const stats = useDashboardStats();
  const revenue = useRevenueChart({ days: 30 });
  const proposals = useProposalsChart({ days: 30 });
  if (stats.isLoading) return <section className="admin-subsection"><div className="admin-loading">Loading dashboard…</div></section>;
  if (stats.isError) return <section className="admin-subsection"><div className="admin-form-error">Unable to load dashboard.</div></section>;
  const data = stats.data ?? {};
  return <section className="admin-subsection"><span className="app-shell__eyebrow">Operations</span><h1>Admin dashboard</h1><div className="admin-stats-grid"><AdminStatsCard label="Revenue" value={formatMoney(data.totalRevenueUsd)} /><AdminStatsCard label="Pending payouts" value={formatMoney(data.totalPendingPayoutsUsd)} /><AdminStatsCard label="Active users" value={data.activeUsers ?? 0} /><AdminStatsCard label="Completed contracts" value={data.completedContracts ?? 0} /></div><div className="admin-data-grid"><section className="admin-subsection"><h2>Revenue</h2>{revenue.isError ? <div className="admin-form-error">Revenue chart unavailable.</div> : <div className="admin-chart-list">{(revenue.data?.items ?? []).map((row) => <div key={row.date}><span>{row.date}</span><strong>{formatMoney(row.revenueUsd)}</strong></div>)}</div>}</section><section className="admin-subsection"><h2>Proposals</h2>{proposals.isError ? <div className="admin-form-error">Proposals chart unavailable.</div> : <div className="admin-chart-list">{(proposals.data?.items ?? []).map((row) => <div key={row.date}><span>{row.date}</span><strong>{row.submitted} submitted</strong></div>)}</div>}</section></div></section>;
}
