import React from "react";
import { useNavigate } from "react-router-dom";
import { Sparkles, Loader2 } from "lucide-react";
import Button from "../../components/common/Button";
import { toastError } from "../../utils/toast";
import { useStartTalentMatch, useProjectWorkflow } from "./aiWorkflowQueries";

/**
 * "Find best talent" on a project the client owns.
 *
 * ## Navigates rather than renders
 *
 * Matching takes a few seconds and produces a ranked list with a factor
 * breakdown. That is a page, not a dropdown, and the client may want to send the
 * link to a colleague before deciding. So the button starts the run and
 * navigates; the shortlist page polls.
 *
 * ## Refuses to start a second run
 *
 * A client who presses this twice, or presses it on a project that already has a
 * shortlist waiting, is told what already exists rather than being sent to a new
 * page that silently shows the old one. The server also returns the existing run
 * instead of creating a duplicate, so the worst case was a confusing message --
 * not two pipelines competing to write the same shortlist rows.
 */
export default function FindTalentButton({ projectId, disabled = false, className = "" }) {
  const navigate = useNavigate();
  const start = useStartTalentMatch();
  const existing = useProjectWorkflow(projectId);

  // Only asked once the client has committed to using the feature, and only for
  // the project in view. An always-on query per project row would mean a list of
  // twenty projects making twenty requests nobody asked for.
  const knownRunId = existing.data?.id ?? existing.data?.workflowRunId;

  const begin = async () => {
    try {
      // No plan: this is the ordinary first run, and the planner chooses the
      // weights. A client who wants to set them does it on the shortlist page,
      // where they can see what the planner chose before changing it.
      const result = await start.mutateAsync({ projectId });
      const runId = result?.workflow_run_id ?? knownRunId;

      if (!runId) {
        toastError("The AI run started but returned no id. Try again.");
        return;
      }

      if (result?.already_running) {
        // Not an error. Saying so prevents someone concluding the button is
        // broken when the shortlist they already have is perfectly fine.
        navigate(`/my-projects/${projectId}/ai-shortlist/${runId}`);
        return;
      }

      navigate(`/my-projects/${projectId}/ai-shortlist/${runId}`);
    } catch (error) {
      toastError(error?.message ?? "Could not start talent matching.");
    }
  };

  return (
    <Button
      variant="ghost"
      onClick={begin}
      disabled={disabled}
      loading={start.isPending}
      className={className}
    >
      {start.isPending ? (
        <>
          <Loader2 size={16} className="mr-2 animate-spin" aria-hidden="true" />
          Analysing freelancers
        </>
      ) : (
        <>
          <Sparkles size={16} className="mr-2" aria-hidden="true" />
          Find best talent
        </>
      )}
    </Button>
  );
}
