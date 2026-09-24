import { useState } from "react";
import { useParams, useNavigate } from "react-router-dom";
import { useAdminDisputeDetail } from "./disputeQueries.js";
import { useResolveDispute } from "./disputeMutations.js";

export default function DisputeDetail() {
  const { id } = useParams();
  const navigate = useNavigate();
  const { data: dispute, isLoading, isError } = useAdminDisputeDetail(id);
  const resolve = useResolveDispute();
  const [notes, setNotes] = useState("");
  if (isLoading) return <section className="admin-subsection"><div className="admin-loading">Loading dispute case...</div></section>;
  if (isError || !dispute) return <section className="admin-subsection"><div className="admin-form-error" role="alert">Case record unavailable.</div><button className="button button--quiet" onClick={() => navigate("/admin/disputes")}>Back to disputes</button></section>;
  return <section className="admin-subsection"><span className="app-shell__eyebrow">Dispute #{dispute.id}</span><h1>{dispute.reason}</h1><p>{dispute.description}</p><p>Status: <strong>{dispute.status}</strong></p><p>Opened by: {dispute.openerRole || "Unknown"}</p>{dispute.evidenceUrls?.length > 0 && <ul>{dispute.evidenceUrls.map((url) => <li key={url}><a href={url} target="_blank" rel="noreferrer">Evidence</a></li>)}</ul>}{dispute.status === "Pending" || dispute.status === "UnderReview" ? <form className="form-card" onSubmit={(event) => { event.preventDefault(); resolve.mutate({ id: Number(id), status: "Resolved", adminNotes: notes }); }}><label>Admin notes<textarea required value={notes} onChange={(event) => setNotes(event.target.value)} /></label><div className="button-row"><button className="button button--quiet" type="button" onClick={() => resolve.mutate({ id: Number(id), status: "Rejected", adminNotes: notes })}>Reject dispute</button><button className="button button--accent" disabled={resolve.isPending}>Resolve dispute</button></div></form> : <p>{dispute.adminNotes || "No resolution notes."}</p>}</section>;
}
