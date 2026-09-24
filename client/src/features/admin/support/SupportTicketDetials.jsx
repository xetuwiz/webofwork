import { useState } from "react";
import { useParams, useNavigate } from "react-router-dom";
import { useTicketDetails } from "./supportQueries.js";
import { useReplyMutation } from "./supportMutations.js";

export default function SupportTicketDetail() {
  const { id } = useParams();
  const navigate = useNavigate();
  const { data: ticket, isLoading, isError } = useTicketDetails(id);
  const reply = useReplyMutation();
  const [message, setMessage] = useState("");
  if (isLoading) return <section className="admin-subsection"><div className="admin-loading">Loading ticket…</div></section>;
  if (isError || !ticket) return <section className="admin-subsection"><div className="admin-form-error">Ticket unavailable.</div></section>;
  return <section className="admin-subsection"><button className="button button--quiet" onClick={() => navigate("/admin/support")}>Back to tickets</button><h1>{ticket.subject}</h1><p>{ticket.message}</p><p>Status: <strong>{ticket.status}</strong></p>{ticket.adminReply && <div className="form-card"><strong>Admin reply</strong><p>{ticket.adminReply}</p></div>}<form className="form-card" onSubmit={(e) => { e.preventDefault(); reply.mutate({ id: Number(id), reply: message }, { onSuccess: () => setMessage("") }); }}><h2>Reply to user</h2><textarea required value={message} onChange={(e) => setMessage(e.target.value)} /><button className="button button--accent" disabled={reply.isPending}>Send reply</button></form></section>;
}
