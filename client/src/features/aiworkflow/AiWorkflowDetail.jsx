import { useParams, useNavigate } from "react-router-dom";
import { useAiWorkflow } from "./aiWorkflowQueries.js";
import { useCancelAiWorkflow } from "./aiWorkflowMutations.js";
import WorkflowApprovalPanel from "./WorkflowApprovalPanel.jsx";

export default function AiWorkflowDetail() {  const { id } = useParams();
  const navigate = useNavigate();
  const { data: workflow, isLoading, isError } = useAiWorkflow(id);
  const cancel = useCancelAiWorkflow();
  if (isLoading) return <section className="admin-subsection"><div className="admin-loading">Loading workflow…</div></section>;
  if (isError || !workflow) return <section className="admin-subsection"><div className="admin-form-error">Workflow unavailable.</div></section>;
  return <section className="admin-subsection"><button className="button button--quiet" onClick={() => navigate("/admin/ai-workflows")}>Back to monitor</button><h1>Workflow #{workflow.id}</h1><p>{workflow.objective}</p><p>Status: <strong>{workflow.status}</strong></p><h2>Step log</h2><div className="admin-payout-list">{(workflow.stepLogs || workflow.steps || []).map((step) => <article className="admin-payout-row" key={step.id}><div><strong>{step.stepName}</strong><small>{step.status} · {step.durationMs ?? 0}ms</small></div><span>{step.output}</span></article>)}</div><WorkflowApprovalPanel workflow={workflow} />{["Pending", "Running", "PendingApproval"].includes(String(workflow.status)) && <button className="button button--quiet" onClick={() => cancel.mutate(Number(id))} disabled={cancel.isPending}>Cancel workflow</button>}</section>;
}
