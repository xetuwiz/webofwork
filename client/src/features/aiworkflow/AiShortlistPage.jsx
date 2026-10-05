import React, { useMemo } from "react";
import { useParams, Link, useNavigate } from "react-router-dom";
import { Loader2, ArrowLeft, Sparkles } from "lucide-react";
import ShortlistApprovalPanel from "./ShortlistApprovalPanel";
import WeightPanel from "./WeightPanel";
import { useAiWorkflow, useShortlist, useStartTalentMatch } from "./aiWorkflowQueries";
import {
  describeStatus,
  isTerminalStatus,
  normaliseWorkflowStatus,
} from "./workflowDisplay";
import { toastSuccess, toastError } from "../../utils/toast";

/**
 * The shortlist page: a run's progress, and the client's decision about it.
 *
 * ## One run, one page
 *
 * The workflow id is in the route rather than looked up from the project. Looking
 * it up would mean two clients on the same project silently sharing one page --
 * one person's pending approval showing up in another's browser, and an approve
 * button acting on a decision that is not theirs.
 */
export default function AiShortlistPage() {
  // The route parameter is `:id`, not `:projectId`
  // (`/my-projects/:id/ai-shortlist/:workflowRunId`). Reading `projectId` here
  // yielded `undefined`, so "Re-run with these weights" posted to
  // `/ai/talent-match/start/undefined` and 404'd, and the back-link pointed at
  // `/my-projects/undefined`. Aliasing on read keeps the local name honest
  // without renaming the parameter in one place and missing it in another.
  const { id: projectId, workflowRunId } = useParams();
  const navigate = useNavigate();

  const run = useAiWorkflow(workflowRunId);
  const shortlist = useShortlist(workflowRunId, {
    // No point asking for a shortlist before there is a run to ask about.
    enabled: Boolean(workflowRunId) && Boolean(run.data),
  });

  const rerun = useStartTalentMatch();

  // Normalised here as well as at the query boundary. A component that is only
  // correct while some upstream caller happened to tidy its input is not correct,
  // and the cache can be populated from a mutation, a prefetch, or a test without
  // going near `queryFn`.
  const status = normaliseWorkflowStatus(run.data?.status);
  const copy = describeStatus(status);
  const finished = isTerminalStatus(status);

  // The agent's own words for this run, when it has any.
  //
  // `final_output` is a jsonb column, so it arrives as a string and is parsed here
  // rather than in the component. A parse failure yields null rather than throwing:
  // this is a supplementary sentence, and failing the page over it would hide the
  // shortlist the client came for.
  //
  // `clarification_question` is the one that matters. When the planner or the
  // drafter stops to ask the client something, the page used to say only that it
  // was waiting -- so the client was told to clarify without being told what to
  // clarify, and the run could not be moved forward from the screen at all.
  const payload = useMemo(() => {
    const raw = run.data?.final_output ?? run.data?.finalOutput;
    if (!raw) return null;
    if (typeof raw === "object") return raw;
    try {
      return JSON.parse(raw);
    } catch {
      return null;
    }
  }, [run.data?.final_output, run.data?.finalOutput]);

  const clarificationQuestion =
    payload?.clarification_question ?? payload?.clarificationQuestion ?? null;

  /**
   * Re-run with the client's weights.
   *
   * A re-run creates a *new* run rather than mutating this one, so the route
   * changes. The old run's shortlist is superseded but still on record, and
   * saying so is the difference between "your list changed" and a list silently
   * swapping out from under someone who may be reading it.
   */
  const rerunWithWeights = async (plan) => {
    try {
      const result = await rerun.mutateAsync({ projectId, plan });

      const superseded = result?.superseded_workflow_run_ids ?? [];
      if (superseded.length > 0) {
        toastSuccess(
          `Re-running. The previous shortlist (${superseded.length} run${
            superseded.length === 1 ? "" : "s"
          }) has been replaced.`
        );
      } else {
        toastSuccess("Re-running with your weights.");
      }

      // The new run owns the shortlist now, so this page's run id is stale.
      if (result?.workflow_run_id) {
        navigate(`/my-projects/${projectId}/ai-shortlist/${result.workflow_run_id}`);
      }
    } catch (error) {
      toastError(error?.message ?? "Could not re-run with those weights.");
    }
  };

  return (
    <div className="max-w-4xl mx-auto space-y-6">
      <div>
        <Link
          to={`/my-projects/${projectId}`}
          className="inline-flex items-center gap-1.5 text-sm text-fg-muted hover:text-fg"
        >
          <ArrowLeft size={16} aria-hidden="true" />
          Back to project
        </Link>
        <h1 className="text-2xl font-bold text-fg mt-3 flex items-center gap-2">
          <Sparkles size={22} className="text-brand" aria-hidden="true" />
          Find best talent
        </h1>
      </div>

      <StatusPanel
        status={status}
        copy={copy}
        isLoading={run.isLoading}
        isError={run.isError}
        // snake_case, because that is what the API sends. `Program.cs` sets
        // `PropertyNamingPolicy = SnakeCaseLower`, so a C# `ApprovalDecision`
        // serialises as `approval_decision` -- and there is no camelCase transform
        // on the axios response to paper over the difference. Read as camelCase
        // these were always undefined, so a recorded decision never appeared.
        summary={run.data?.approval_rationale ?? run.data?.approvalRationale}
        rationale={run.data?.approval_decision ?? run.data?.approvalDecision}
      />

      {/* The question the AI is actually asking.
          Rendered above the shortlist because a run stopped for clarification has
          no shortlist to show, and the question is the only actionable thing on the
          page. */}
      {clarificationQuestion && (
        <section className="rounded-2xl border border-brand-line bg-brand-soft p-6 sm:p-8">
          <div className="flex items-start gap-3">
            <Sparkles size={18} className="text-brand shrink-0 mt-0.5" aria-hidden="true" />
            <div className="min-w-0">
              <h2 className="font-semibold text-fg text-sm">The AI needs one more thing</h2>
              <p className="text-fg text-sm mt-2 leading-relaxed">
                {clarificationQuestion}
              </p>
              <p className="text-fg-muted text-xs mt-3">
                Add the detail to your project and run this again — the answer is not
                read from this page.
              </p>
            </div>
          </div>
        </section>
      )}

      {shortlist.isLoading && (
        <div className="py-12 text-center text-fg-muted text-sm">
          <Loader2 size={20} className="mx-auto mb-3 animate-spin" aria-hidden="true" />
          Scoring the freelancer pool
        </div>
      )}

      {!shortlist.isLoading && shortlist.data && (
        <>
          {/* `no_match_reason`, as the API sends it. This read as `noMatchReason`, so
            the panel could never have rendered even once the server started sending
            the key -- same casing mismatch as `approval_decision` above. */}
          {(shortlist.data.no_match_reason ?? shortlist.data.noMatchReason) && (
            <div className="rounded-2xl border border-warning-line bg-warning-soft p-6">
              <h2 className="font-semibold text-warning-fg text-sm">No matches found</h2>
              <p className="text-warning-fg text-sm mt-1.5 leading-relaxed">
                {shortlist.data.no_match_reason ?? shortlist.data.noMatchReason}
              </p>
            </div>
          )}

          <ShortlistApprovalPanel
            workflowRunId={workflowRunId}
            status={status}
            shortlist={shortlist.data.shortlist ?? []}
          />
        </>
      )}

      {/* The weights sit below the shortlist, not above it. The list is what the
          client came for; the weights are the explanation of it, and a panel of
          five sliders above the answer would bury the answer. */}
      {shortlist.data && (
        <WeightPanel
          plan={shortlist.data.plan}
          planSource={shortlist.data.plan_source}
          onRerun={rerunWithWeights}
          isRerunning={rerun.isPending}
          // Once the shortlist is approved the people are being contacted, and
          // swapping the list underneath that is not a re-run, it is a mistake.
          disabled={status === "Approved" || status === "Completed"}
        />
      )}

      {shortlist.isError && (
        <div className="rounded-2xl border border-danger-line bg-danger-soft p-6">
          <h2 className="font-semibold text-danger-fg text-sm">Could not load the shortlist</h2>
          <p className="text-danger-soft-fg text-sm mt-1.5">
            Nothing has been sent to anyone. Go back and start again.
          </p>
        </div>
      )}

      {finished && status === "Failed" && (
        <p className="text-sm text-fg-muted">
          This run failed. Nothing was contacted, and starting again is safe.
        </p>
      )}
    </div>
  );
}

/**
 * Progress, in words.
 *
 * The states are the .NET enum names, and they are shown as prose. A client being
 * asked to approve something should not have to know what
 * "PendingClientApproval" means.
 */
function StatusPanel({ status, copy, isLoading, isError, summary, rationale }) {
  if (isLoading) {
    return (
      <div className="rounded-2xl border border-line bg-surface p-6 flex items-center gap-3">
        <Loader2 size={18} className="text-brand animate-spin" aria-hidden="true" />
        <span className="text-sm text-fg-muted">Checking progress</span>
      </div>
    );
  }

  if (isError) {
    return (
      <div className="rounded-2xl border border-danger-line bg-danger-soft p-6">
        <h2 className="font-semibold text-danger-fg text-sm">Could not load this run</h2>
        <p className="text-danger-soft-fg text-sm mt-1.5">
          It may have been cancelled, or the link may be wrong.
        </p>
      </div>
    );
  }

  return (
    <section className="rounded-2xl border border-line bg-surface p-6 sm:p-8">
      <div className="flex items-start gap-3">
        {!isDone(status) && (
          <Loader2 size={18} className="text-brand animate-spin shrink-0 mt-0.5" aria-hidden="true" />
        )}
        <div className="min-w-0">
          <h2 className="font-semibold text-fg text-sm">{copy.label}</h2>
          <p className="text-fg-muted text-sm mt-1 leading-relaxed">{copy.blurb}</p>

          {summary && (
            <p className="text-fg-muted text-sm mt-3 leading-relaxed border-l-2 border-line pl-3">
              {summary}
            </p>
          )}
          {rationale && (
            <p className="text-fg-subtle text-xs mt-2">Decision: {rationale}</p>
          )}
        </div>
      </div>
    </section>
  );
}

const isDone = (status) => isTerminalStatus(status) || status === "Approved";
