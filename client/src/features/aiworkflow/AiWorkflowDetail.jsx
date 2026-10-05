/**
 * One AI run, and the manager's decision about it.
 *
 * ## This is the second gate
 *
 * Approving here is not a review step. `POST /approve` is what creates the
 * contract the plan describes, and it is the only route to that contract. So
 * this screen is the second human-in-the-loop gate, and it has to make three
 * things legible before an admin presses anything:
 *
 *  - what the run decided, and on what evidence;
 *  - what approving will actually create, in the client's own terms;
 *  - and that rejecting is not a rejection of the client's project.
 *
 * The third matters most and is easiest to get wrong. An admin looking at a
 * failed validation on someone else's project is not judging the client, and a
 * button labelled "Reject" invites that reading.
 */

import React, { useState } from "react";
import { useParams, Link } from "react-router-dom";
import { AlertTriangle, ArrowLeft, Check, Loader2, X } from "lucide-react";
import Button from "../../components/common/Button";
import Dialog from "../../components/common/Dialog";
import { toastSuccess, toastError } from "../../utils/toast";
import { useAiWorkflow, useApproveWorkflow } from "./aiWorkflowQueries";
import { describeStatus, formatScore, canApproveContract, isTerminalStatus } from "./workflowDisplay";

export default function AiWorkflowDetail() {
  const { id } = useParams();

  const run = useAiWorkflow(id);
  const approve = useApproveWorkflow();

  const [decision, setDecision] = useState(null);
  const [rationale, setRationale] = useState("");

  const status = run.data?.status;
  const copy = describeStatus(status);
  const canAct = canApproveContract(status);

  const submit = async () => {
    try {
      await approve.mutateAsync({
        id: Number(id),
        decision,
        rationale: rationale.trim() || undefined,
      });

      toastSuccess(
        decision === "approve"
          ? "Approved. The contract has been created."
          : "Rejected. Nothing was created."
      );
      setDecision(null);
      setRationale("");
    } catch (error) {
      toastError(error?.message ?? "Could not record that decision.");
      setDecision(null);
    }
  };

  if (run.isLoading) {
    return (
      <div className="p-8 flex items-center gap-2 text-fg-muted text-sm">
        <Loader2 size={18} className="animate-spin" aria-hidden="true" />
        Loading the run
      </div>
    );
  }

  if (run.isError || !run.data) {
    return (
      <div className="p-8 space-y-4">
        <div className="rounded-2xl border border-danger-line bg-danger-soft p-6">
          <h1 className="font-semibold text-danger-fg text-sm">Could not load this run</h1>
          <p className="text-danger-soft-fg text-sm mt-1.5">
            It may have been cancelled, or the link may be wrong. Nothing has been
            created either way.
          </p>
        </div>
        <Link to="/admin/ai-workflows" className="text-sm text-fg-muted hover:text-fg">
          Back to all runs
        </Link>
      </div>
    );
  }

  const data = run.data;
  const output = parseOutput(data.finalOutput);
  const issues = output?.validation?.issues ?? [];

  return (
    <div className="p-6 lg:p-8 space-y-6 max-w-4xl">
      <div>
        <Link
          to="/admin/ai-workflows"
          className="inline-flex items-center gap-1.5 text-sm text-fg-muted hover:text-fg"
        >
          <ArrowLeft size={16} aria-hidden="true" />
          All runs
        </Link>
        <h1 className="text-2xl font-bold text-fg mt-3">{data.objective || `Run #${data.id}`}</h1>
        <p className="text-sm text-fg-muted mt-1">
          Run #{data.id} &middot; stage {data.stage} &middot; {copy.label}
        </p>
      </div>

      <section className="bg-surface rounded-2xl border border-line p-6">
        <h2 className="font-semibold text-fg text-sm">Summary</h2>
        <p className="text-fg-muted text-sm mt-1.5 leading-relaxed">
          {output?.summary ?? "No summary was recorded for this run."}
        </p>
      </section>

      {issues.length > 0 && (
        <section className="rounded-2xl border border-warning-line bg-warning-soft p-6">
          <h2 className="font-semibold text-warning-fg text-sm flex items-center gap-2">
            <AlertTriangle size={16} aria-hidden="true" />
            What the safety check found
          </h2>
          <ul className="mt-3 space-y-2">
            {issues.map((issue, index) => (
              <li key={index} className="text-sm text-warning-fg">
                <span className="font-semibold">{issue.severity}</span>: {issue.message}
              </li>
            ))}
          </ul>
        </section>
      )}

      {Array.isArray(output?.recommendations) && output.recommendations.length > 0 && (
        <section className="bg-surface rounded-2xl border border-line overflow-hidden">
          <h2 className="font-semibold text-fg text-sm p-6 border-b border-line-soft">
            The shortlist this run produced
          </h2>
          <ul className="divide-y divide-line-soft">
            {output.recommendations.map((rec) => (
              <li key={rec.freelancer_id ?? rec.freelancerId} className="p-6">
                <p className="text-sm font-semibold text-fg">
                  {formatScore(rec.final_score)} match
                </p>
                {rec.reason && (
                  <p className="text-fg-muted text-sm mt-1.5 leading-relaxed">{rec.reason}</p>
                )}
              </li>
            ))}
          </ul>
        </section>
      )}

      <section className="bg-surface rounded-2xl border border-line p-6">
        <h2 className="font-semibold text-fg text-sm">Decision</h2>

        {canAct ? (
          <>
            <p className="text-fg-muted text-sm mt-1.5 leading-relaxed">
              Approving creates the contract this plan describes. Rejecting closes
              the run and creates nothing -- it is a decision about this run, not
              about the client&apos;s project.
            </p>

            <div className="mt-4 flex flex-col sm:flex-row gap-3">
              <Button
                variant="primary"
                onClick={() => setDecision("approve")}
                disabled={approve.isPending}
              >
                <Check size={16} className="mr-2" aria-hidden="true" />
                Approve and create the contract
              </Button>
              <Button variant="ghost" onClick={() => setDecision("reject")} disabled={approve.isPending}>
                <X size={16} className="mr-2" aria-hidden="true" />
                Reject this run
              </Button>
            </div>
          </>
        ) : (
          <p className="text-fg-muted text-sm mt-1.5">
            {isTerminalStatus(status)
              ? `This run is ${copy.label.toLowerCase()} and cannot be decided again.`
              : `This run is not waiting on an admin. ${copy.blurb}`}
          </p>
        )}

        {/* snake_case: `Program.cs` sets `PropertyNamingPolicy = SnakeCaseLower`, so a
            C# `ApprovalDecision` arrives as `approval_decision`, and no camelCase
            transform exists on the axios response. Read as camelCase, the whole
            decision block below was unreachable -- a run that had been approved
            showed nothing at all about who approved it. */}
        {data.approval_decision && (
          <p className="text-fg-subtle text-xs mt-4">
            Recorded: {data.approval_decision}
            {data.approved_at ? ` on ${new Date(data.approved_at).toLocaleString()}` : ""}
            {data.approval_rationale ? ` — ${data.approval_rationale}` : ""}
          </p>
        )}
      </section>

      <Dialog
        open={decision !== null}
        onClose={() => setDecision(null)}
        title={decision === "approve" ? "Approve and create the contract?" : "Reject this run?"}
        description={
          decision === "approve"
            ? "This creates a contract. It is not a draft."
            : "Nothing is created and the client is not contacted."
        }
        size="sm"
      >
        <div className="space-y-4">
          <label className="block text-sm font-semibold text-fg" htmlFor="ai-rationale">
            Why
            <span className="font-normal text-fg-muted"> (optional, but recorded)</span>
          </label>
          <textarea
            id="ai-rationale"
            value={rationale}
            onChange={(e) => setRationale(e.target.value)}
            rows={3}
            className="w-full px-3 py-2 border-2 border-line-strong rounded-lg text-sm"
          />

          <div className="flex justify-end gap-3">
            <Button variant="ghost" onClick={() => setDecision(null)} disabled={approve.isPending}>
              Cancel
            </Button>
            <Button
              variant={decision === "approve" ? "primary" : "ghost"}
              onClick={submit}
              loading={approve.isPending}
            >
              {approve.isPending && (
                <Loader2 size={16} className="mr-2 animate-spin" aria-hidden="true" />
              )}
              {decision === "approve" ? "Approve" : "Reject"}
            </Button>
          </div>
        </div>
      </Dialog>
    </div>
  );
}

/**
 * The agent's response, if there is one to read.
 *
 * `WorkflowRun.FinalOutput` holds the raw envelope as text, so it is parsed here
 * rather than returned as an object. A run that failed, or one whose envelope is
 * unparseable, shows the summary line instead of a blank page -- the run exists
 * either way and the admin needs to be able to read it.
 */
function parseOutput(raw) {
  if (!raw) return null;
  if (typeof raw === "object") return raw;
  try {
    return JSON.parse(raw);
  } catch {
    return null;
  }
}
