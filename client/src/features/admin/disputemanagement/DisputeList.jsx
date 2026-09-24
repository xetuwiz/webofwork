import { useState } from "react";
import { useAdminDisputes } from "./disputeQueries.js";
import { useNavigate } from "react-router-dom";

export default function DisputeList() {
  const navigate = useNavigate();
  const [status, setStatus] = useState("");
  const { data, isLoading, isError } = useAdminDisputes(status ? { status } : {});
  const disputes = data?.results || (Array.isArray(data) ? data : []);
  return <section className="admin-subsection"><div className="admin-subsection__heading"><div><span className="app-shell__eyebrow">Trust & safety</span><h1>Dispute resolution center</h1></div><span>{data?.count ?? disputes.length} cases</span></div><div className="button-row">{["", "Pending", "UnderReview", "Resolved", "Rejected"].map((item) => <button key={item || "all"} className={`button ${status === item ? "button--accent" : "button--quiet"}`} onClick={() => setStatus(item)}>{item || "All"}</button>)}</div>{isLoading && <div className="admin-loading">Loading disputes...</div>}{isError && <div className="admin-form-error" role="alert">Unable to load disputes. Please try again.</div>}{!isLoading && !isError && disputes.length === 0 && <div className="admin-empty">No disputes match the selected filter.</div>}<div className="admin-payout-list">{disputes.map((dispute) => <article className="admin-payout-row" key={dispute.id}><div><strong>Dispute #{dispute.id}</strong><small>{dispute.reason} · {dispute.openerRole || "Unknown opener"}</small></div><div><strong>{dispute.status}</strong><button className="button button--quiet" onClick={() => navigate(`/admin/disputes/${dispute.id}`)}>View case</button></div></article>)}</div></section>;
}
