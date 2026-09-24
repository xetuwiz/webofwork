import { useState } from "react";
import { useTicketDetails, useSupportTickets } from "./supportQueries.js";
import { useReplyMutation } from "./supportMutations.js";
import { useNavigate } from "react-router-dom";

export default function SupportTicketList() {
  const navigate = useNavigate();
  const [status, setStatus] = useState("");
  const { data, isLoading, isError } = useSupportTickets(status ? { status } : {});
  const rows = data?.items || data?.results || (Array.isArray(data) ? data : []);
  return <section className="admin-subsection"><div className="admin-subsection__heading"><div><span className="app-shell__eyebrow">Trust & safety</span><h1>Support tickets</h1></div><span>{data?.totalCount ?? rows.length} tickets</span></div><select className="input" value={status} onChange={(e) => setStatus(e.target.value)}><option value="">All statuses</option>{["Open", "Resolved", "Closed"].map((item) => <option key={item}>{item}</option>)}</select>{isLoading && <div className="admin-loading">Loading support tickets…</div>}{isError && <div className="admin-form-error">Unable to load support tickets.</div>}{!isLoading && !isError && rows.length === 0 && <div className="admin-empty">No support tickets found.</div>}<div className="admin-payout-list">{rows.map((ticket) => <article className="admin-payout-row" key={ticket.id}><div><strong>{ticket.subject}</strong><small>{ticket.userEmail || "User"} · {ticket.status}</small></div><button className="button button--quiet" onClick={() => navigate(`/admin/support/${ticket.id}`)}>View ticket</button></article>)}</div></section>;
}
