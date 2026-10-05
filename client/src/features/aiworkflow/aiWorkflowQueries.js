/**
 * Queries and mutations for the AI workflow surface.
 *
 * ## The polling rule
 *
 * A run is asynchronous relative to the person who started it. `draft` and
 * `talent_match` both return a run id and fill it in later, so both are polled
 * -- but only while there is something to wait for. A query that keeps polling a
 * finished run is traffic for a screen nobody is looking at, and a shortlist
 * that repaints every few seconds makes the approve button feel unsafe to press.
 *
 * So polling stops on a terminal state, and `refetchInterval` returns `false` to
 * say "not at all" rather than a number.
 */

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import {
  approveShortlist,
  approveWorkflow,
  cancelWorkflow,
  fetchDraft,
  fetchShortlist,
  fetchWorkflow,
  fetchWorkflowStatus,
  fetchWorkflows,
  requestDraft,
  resumeWorkflow,
  startTalentMatch,
  startWorkflow,
} from "./aiWorkflowApi";
import { normaliseWorkflowStatus, TERMINAL_STATUSES } from "./workflowDisplay";

/**
 * Settle the status casing on the way in.
 *
 * The server serialises every enum as snake_case (a global
 * `JsonStringEnumConverter(SnakeCaseLower)`), while every comparison in this
 * feature is written against the C# spelling. Normalising here, at the boundary,
 * fixes the label, the approve control, terminal-state polling and the failure
 * notice in one place -- rather than each consumer having to remember.
 *
 * Returns a new object rather than mutating, because React Query holds onto the
 * response and a mutation here would be invisible to its own change detection.
 */
const withNormalisedStatus = (data) =>
  data && typeof data === "object"
    ? { ...data, status: normaliseWorkflowStatus(data.status) }
    : data;

/** States a run never leaves. Polling these is pure waste. */
// Shares the exported set rather than keeping a second copy that can drift from it.

/**
 * The only statuses that mean "still moving on its own".
 *
 * An allow-list rather than a deny-list, which is the opposite of what this
 * function did before it was tested: it polled anything that was not terminal
 * and not awaiting a human, so a status added in C# after this shipped was
 * polled every three seconds until the tab closed.
 *
 * The failure that matters is the one an unknown status causes. Polling a run
 * that has actually finished means a spinner that never stops and a request
 * every three seconds for as long as the page is open, and neither looks like a
 * bug to the user -- it looks like the server being slow. Stopping instead
 * leaves a stale page, which is honest and which the user can refresh.
 */
const IN_PROGRESS = new Set(["Pending", "Running"]);

/**
 * How often to re-check, given a status.
 *
 * @returns {number|false} A delay in ms, or false to stop polling.
 */
export function pollIntervalFor(status) {
  // No status yet, so nothing to re-check. React Query fires this with
  // `undefined` before the first response arrives.
  if (!status) return false;
  // Waiting on a human is not "in progress". Polling every 3s while a client
  // reads a shortlist is exactly the wrong use of the request, and it
  // repaints the page under the approve button, which makes it feel unsafe to
  // press.
  // Normalised here as well as at the query boundary: this function decides
  // whether to keep polling, and polling a finished run forever is a silent
  // failure that costs a request every few seconds indefinitely.
  const state = normaliseWorkflowStatus(status);

  if (state === "PendingClientApproval" || state === "PendingManagerApproval") return false;
  // `PendingApproval` is the pre-split status meaning "waiting for a decision".
  // It is a human gate too, and predates the migration, so it is matched here
  // rather than being polled.
  if (state === "PendingApproval") return false;
  if (TERMINAL_STATUSES.has(state)) return false;

  return IN_PROGRESS.has(state) ? 3000 : false;
}

/** True when this run is the one the named human has to act on. */
export const isWaitingOnClient = (status) => status === "PendingClientApproval";
export const isWaitingOnManager = (status) => status === "PendingManagerApproval";

// ── queries ────────────────────────────────────────────────────────────

export function useAiWorkflow(id, { enabled = true } = {}) {
  return useQuery({
    queryKey: ["ai-workflow", id],
    queryFn: async () => withNormalisedStatus(await fetchWorkflow(id)),
    enabled: Boolean(id) && enabled,
    refetchInterval: ({ state }) => pollIntervalFor(state.data?.status),
  });
}

export function useProjectWorkflow(projectId) {
  return useQuery({
    queryKey: ["ai-workflow", "project", projectId],
    queryFn: async () => withNormalisedStatus(await fetchWorkflowStatus(projectId)),
    enabled: Boolean(projectId),
  });
}

export function useAiWorkflows(filters = {}) {
  return useQuery({
    queryKey: ["ai-workflows", filters],
    queryFn: async () => withNormalisedStatus(await fetchWorkflows(filters)),
  });
}

export function useDraft(workflowRunId, { enabled = true } = {}) {
  return useQuery({
    queryKey: ["ai-draft", workflowRunId],
    queryFn: () => fetchDraft(workflowRunId),
    enabled: Boolean(workflowRunId) && enabled,
    // `completed` is the server's own answer rather than a guess from the
    // status enum, so a new terminal state cannot leave this polling forever.
    refetchInterval: ({ state }) => (state.data?.completed ? false : 2500),
  });
}

export function useShortlist(workflowRunId, { enabled = true } = {}) {
  return useQuery({
    queryKey: ["ai-shortlist", workflowRunId],
    queryFn: async () => withNormalisedStatus(await fetchShortlist(workflowRunId)),
    enabled: Boolean(workflowRunId) && enabled,
    // The shortlist does not change on its own, so there is nothing to poll
    // for. It is refetched after an approval instead.
    refetchInterval: false,
  });
}

// ── mutations ──────────────────────────────────────────────────────────

/**
 * Invalidates the run, the project run and the shortlist together.
 *
 * All three, because an approval changes all three: the run's status moves, the
 * project's latest run is a different object, and the shortlist is no longer
 * awaiting approval. Invalidating only the run leaves a page that says "approve"
 * over a shortlist that has already been sent -- which is how a client invites
 * the same five people twice.
 */
function useWorkflowInvalidation() {
  const client = useQueryClient();
  return (id) => {
    client.invalidateQueries({ queryKey: ["ai-workflow"] });
    client.invalidateQueries({ queryKey: ["ai-workflows"] });
    if (id) {
      client.invalidateQueries({ queryKey: ["ai-workflow", id] });
      client.invalidateQueries({ queryKey: ["ai-shortlist", id] });
    }
    // Invitations are created server-side when a shortlist is approved.
    // The proposals page holds a "sent-invitations" cache scoped by project
    // that has no way to know new rows were written -- so it must be
    // invalidated here, or the page shows nothing until the user refreshes.
    client.invalidateQueries({ queryKey: ["sent-invitations"] });
  };
}

export function useStartWorkflow() {
  const invalidate = useWorkflowInvalidation();
  return useMutation({
    mutationFn: startWorkflow,
    onSuccess: (run) => invalidate(run?.id),
  });
}

export function useApproveShortlist() {
  const invalidate = useWorkflowInvalidation();
  return useMutation({
    mutationFn: approveShortlist,
    onSuccess: (_result, variables) => invalidate(variables?.id),
  });
}

export function useApproveWorkflow() {
  const invalidate = useWorkflowInvalidation();
  return useMutation({
    mutationFn: approveWorkflow,
    onSuccess: (_result, variables) => invalidate(variables?.id),
  });
}

export function useResumeWorkflow() {
  const invalidate = useWorkflowInvalidation();
  return useMutation({
    mutationFn: resumeWorkflow,
    onSuccess: (run) => invalidate(run?.id),
  });
}

export function useCancelWorkflow() {
  const invalidate = useWorkflowInvalidation();
  return useMutation({
    mutationFn: cancelWorkflow,
    onSuccess: (run) => invalidate(run?.id),
  });
}

export function useRequestDraft() {
  return useMutation({ mutationFn: requestDraft });
}

export function useStartTalentMatch() {
  const invalidate = useWorkflowInvalidation();
  return useMutation({
    // Called as `{ projectId }` for a first run, or `{ projectId, plan }` to
    // re-run with the client's own weights.
    mutationFn: ({ projectId, plan }) => startTalentMatch(projectId, plan),
    // Both keys, not just the new run: a re-run supersedes the old one, and that
    // one's shortlist is exactly the query now showing a list the client has been
    // told is replaced.
    onSuccess: (result) => invalidate(result?.workflow_run_id),
  });
}
