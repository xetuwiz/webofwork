import { useState } from "react";
import { useAiWorkflows } from "./aiWorkflowQueries.js";
import { workflowRows } from "./workflowDisplay.js";
import { useNavigate } from "react-router-dom";

export default function AiWorkflowMonitor() {
  const navigate = useNavigate();
  const { data, isLoading, isError } = useAiWorkflows({ page: 1, pageSize: 20 });
  const [status, setStatus] = useState("");
  const rows = workflowRows(data).filter((row) => !status || String(row.status) === status);
  return <section className="admin-subsection"><h1>AI workflow monitor</h1><select className="input" value={status} onChange={(e) => setStatus(e.target.value)}><option value="">All statuses</option>{["Pending", "Running", "PendingApproval", "Approved", "Rejected", "Completed", "Failed"].map((item) => <option key={item}>{item}</option>)}</select>{isLoading && <div className="admin-loading">Loading workflows…</div>}{isError && <div className="admin-form-error">Unable to load AI workflows.</div>}{!isLoading && !isError && rows.length === 0 && <div className="admin-empty">No AI workflows found.</div>}<div className="admin-payout-list">{rows.map((row) => <article className="admin-payout-row" key={row.id}><div><strong>Workflow #{row.id}</strong><small>{row.objective}</small></div><div><strong>{row.status}</strong><button className="button button--quiet" onClick={() => navigate(`/admin/ai-workflows/${row.id}`)}>View</button></div></article>)}</div></section>;
}
