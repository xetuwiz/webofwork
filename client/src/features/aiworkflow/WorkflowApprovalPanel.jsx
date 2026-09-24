import { useState } from "react";
import { workflowState } from "./workflowDisplay.js";
import { useApproveAiWorkflow } from "./aiWorkflowMutations.js";

export default function WorkflowApprovalPanel({ workflow }) {
  const [decision, setDecision] = useState("approve");
  const [rationale, setRationale] = useState("");
  const mutation = useApproveAiWorkflow();
  const state = workflowState(workflow);
  const submit = (event) => { event.preventDefault(); mutation.mutate({ id: workflow.id, decision, rationale }); };
  return state.canApprove ? <form className="form-card" onSubmit={submit}><h2>Approval decision</h2><label>Rationale<textarea required value={rationale} onChange={(e) => setRationale(e.target.value)} /></label><div className="button-row">{["approve", "reject", "request_revision"].map((item) => <button type="button" key={item} className={`button ${decision === item ? "button--accent" : "button--quiet"}`} onClick={() => setDecision(item)}>{item.replace("_", " ")}</button>)}</div><button className="button button--accent" disabled={mutation.isPending}>Submit decision</button></form> : null;
}
