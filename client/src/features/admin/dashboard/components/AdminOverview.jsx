import { useState } from "react";
import { useDashboardStats, useProposalsChart, useRevenueChart } from "../dashboardQueries.js";
import { usePendingPayouts } from "../../payoutmanagement/payoutQueries.js";
import PayoutReleaseModal from "../../payoutmanagement/PayoutReleaseModal.jsx";
import AdminStatsCard from "./AdminStatsCard.jsx";

const usd = (value) => new Intl.NumberFormat("en-US", { style: "currency", currency: "USD" }).format(Number(value ?? 0));

function ChartRows({ rows, kind, loading }) {
  if (loading) return <div className="admin-loading">Loading chart data...</div>;
  if (!rows.length) return <div className="admin-empty">No chart data is available yet.</div>;
  return <div className="admin-chart-list">{rows.slice(-7).map((row) => <div key={row.date}><span>{new Date(row.date).toLocaleDateString()}</span><strong>{kind === "revenue" ? usd(row.revenueUsd) : `${row.submitted} submitted`}</strong><small>{kind === "revenue" ? `${row.totalTransactions} transactions` : `${row.accepted} accepted · ${row.rejected} rejected`}</small></div>)}</div>;
}

export default function AdminOverview() {
  const [selectedPayout, setSelectedPayout] = useState(null);
  const statsQuery = useDashboardStats();
  const payoutQuery = usePendingPayouts();
  const revenueQuery = useRevenueChart({ days: 30 });
  const proposalsQuery = useProposalsChart({ days: 30 });

  if (statsQuery.isLoading) return <section className="admin-page"><div className="admin-loading">Loading platform overview...</div></section>;
  if (statsQuery.isError) return <section className="admin-page"><div className="admin-form-error">Unable to load dashboard metrics.</div></section>;

  const stats = statsQuery.data ?? {};
  const payouts = Array.isArray(payoutQuery.data) ? payoutQuery.data : payoutQuery.data?.items ?? payoutQuery.data?.results ?? [];
  const revenue = Array.isArray(revenueQuery.data) ? revenueQuery.data : revenueQuery.data?.items ?? [];
  const proposals = Array.isArray(proposalsQuery.data) ? proposalsQuery.data : proposalsQuery.data?.items ?? [];

  return <section className="admin-page"><div className="admin-page__heading"><div><span className="app-shell__eyebrow">Operations</span><h1>Platform overview</h1><p>Keep a clear view of activity, revenue, and obligations.</p></div></div><div className="admin-stats-grid"><AdminStatsCard label="Revenue" value={usd(stats.totalRevenueUsd)} detail={`${stats.totalTransactions ?? 0} transactions`} /><AdminStatsCard label="Platform fees" value={usd(stats.totalPlatformFeesUsd)} /><AdminStatsCard label="Pending payouts" value={usd(stats.totalPendingPayoutsUsd)} detail={`${stats.pendingPayoutCount ?? 0} awaiting release`} /><AdminStatsCard label="Active users" value={stats.activeUsers ?? 0} detail={`${stats.totalUsers ?? 0} total accounts`} /><AdminStatsCard label="Open projects" value={stats.openProjects ?? 0} detail={`${stats.totalProjects ?? 0} total projects`} /><AdminStatsCard label="Completed contracts" value={stats.completedContracts ?? 0} /></div><div className="admin-data-grid"><section className="admin-subsection"><div className="admin-subsection__heading"><div><span className="app-shell__eyebrow">Revenue trend</span><h2>Last 30 days</h2></div></div><ChartRows rows={revenue} kind="revenue" loading={revenueQuery.isLoading} /></section><section className="admin-subsection"><div className="admin-subsection__heading"><div><span className="app-shell__eyebrow">Proposal activity</span><h2>Last 30 days</h2></div></div><ChartRows rows={proposals} kind="proposals" loading={proposalsQuery.isLoading} /></section></div><section className="admin-subsection"><div className="admin-subsection__heading"><div><span className="app-shell__eyebrow">Money movement</span><h2>Pending payouts</h2></div><span>{payouts.length} awaiting review</span></div>{payoutQuery.isLoading && <div className="admin-loading">Loading pending payouts...</div>}{payoutQuery.isError && <div className="admin-form-error">Unable to load pending payouts.</div>}{!payoutQuery.isLoading && !payoutQuery.isError && payouts.length === 0 && <div className="admin-empty">No payouts are currently waiting for release.</div>}{payouts.length > 0 && <div className="admin-payout-list">{payouts.map((payout) => <div className="admin-payout-row" key={payout.id}><div><strong>Payment #{payout.id}</strong><small>{payout.status} · {new Date(payout.createdAt).toLocaleDateString()}</small></div><strong>{usd(payout.amount)}</strong><button className="button button--quiet" type="button" onClick={() => setSelectedPayout(payout)}>Review</button></div>)}</div>}</section><PayoutReleaseModal payment={selectedPayout} onClose={() => setSelectedPayout(null)} /></section>;
}
