import { getErrorMessage } from "../../utils/errorMessage";

/**
 * The AI workflow API, as the server actually exposes it.
 *
 * ## Request bodies are snake_case
 *
 * `Program.cs` sets `PropertyNamingPolicy = SnakeCaseLower`, and that policy
 * governs *deserialization* as well as serialization. `PropertyNameCaseInsensitive`
 * defaults to true, but that forgives differences of case only -- an underscore is
 * not a case difference. So a body key of `freelancerIds` does not bind to a C#
 * property `FreelancerIds`; it binds to nothing, silently, and the DTO keeps its
 * default.
 *
 * Three calls here were sending camelCase and losing their data:
 *
 * | sent | bound to | effect |
 * |---|---|---|
 * | `freelancerIds` | nothing | approval arrived with an empty list; 400 "Approving an empty shortlist" while the page said four were selected |
 * | `projectId` | nothing | the run was started with no project, so the agent had no subject to work from |
 * | `contractSpec: {…}` | nothing | the four fields are read flat at the request root, so the manager gate approved the run without ever creating the contract |
 *
 * Single-word keys (`objective`, `stage`, `decision`, `rationale`) are unaffected,
 * which is why every one of these bodies looked entirely plausible.
 *
 * ## What came from the backup and what did not
 *
 * The backup copy of this file was four one-line calls with no error handling.
 * It also called a list endpoint that did not exist.
 *
 * The other bug was an `unwrap` helper that stripped a `{ data }` envelope these
 * controllers never emit (see below).
 *
 * ## The two gates
 *
 * `approve` is the **manager** gate and creates a contract. `approveShortlist` is
 * the **client** gate and sends invitations. They are different decisions with
 * different consequences and different permissions, so they are different calls
 * rather than one call with a flag. Calling the wrong one is refused by the
 * server -- `approve` rejects a run that is waiting on its client, because it
 * would draft a contract with whoever the model ranked first.
 */

import axiosInstance from "../../api/axiosInstance";

// These controllers do NOT wrap their answers in a `data` envelope -- they answer
// `Ok(run)`, `Accepted(new { workflow_run_id = ... })` and so on, bare. So the
// response body is the payload and every read below returns it as-is.
//
// A former `unwrap` helper here assumed an envelope and read `.data.data` off the
// body. That is two unwraps where zero are needed, so every call in this file
// resolved to `undefined`: React Query rejected the undefined and
// start/resume/cancel/approve/shortlist/draft all surfaced as failures while the
// server had answered 200. Checked against AiWorkflowController and
// AiTalentMatchController action by action -- none of them emit an envelope.

/**
 * Surfacing the server's message is the difference between "Check failed" and
 * "Only the client who posted this project can approve its shortlist" -- and the
 * shortlist approval path is exactly where a rejection is most likely and most
 * worth explaining.
 *
 * Delegates to the shared `getErrorMessage` rather than reading three keys
 * here. The local version handled `error`, `detail` and `title`, so it missed
 * the validation `errors` dictionary and the array-of-strings shape that the
 * other endpoints use, and it had no offline/network branch.
 */
const problemMessage = (error, fallback) => getErrorMessage(error, fallback);

// ── lifecycle ──────────────────────────────────────────────────────────

/**
 * Start a run. `stage` is required and validated server-side against the shared
 * vocabulary; an unknown stage is a 400 rather than a silent default, because a
 * silent default would run the wrong agent and report success.
 */
export const startWorkflow = async ({ objective, stage, projectId }) => {
  const { data } = await axiosInstance.post("/ai/workflows/start", {
    objective,
    stage,
    // `project_id`, because that is the key the DTO binds. Sent as `projectId` it
    // was dropped and the run started with no project at all.
    project_id: projectId ?? null,
  });
  return data;
};

export const fetchWorkflowStatus = async (projectId) => {
  const { data } = await axiosInstance.get(`/ai/workflows/by-project/${projectId}`);
  return data;
};

export const fetchWorkflow = async (id) => {
  const { data } = await axiosInstance.get(`/ai/workflows/${id}`);
  return data;
};

/** Admin monitor. Paged; `count` is the total, not the page size. */
export const fetchWorkflows = async ({ stage, status, projectId, page = 1, pageSize = 25 } = {}) => {
  const { data } = await axiosInstance.get("/ai/workflows", {
    params: { stage, status, project_id: projectId, page, page_size: pageSize },
  });
  return data;
};

/** The manager gate. Creates the contract the plan describes. */
export const approveWorkflow = async ({ id, decision, rationale, contractSpec }) => {
  const { data } = await axiosInstance.post(`/ai/workflows/${id}/approve`, {
    decision,
    rationale,
    // Flat at the request root, and snake_case.
    //
    // `ApproveWorkflowRequest` declares FreelancerId, TotalAmount, PackageId and
    // ProjectId as siblings. They were being sent nested under `contractSpec`,
    // which binds to nothing -- so `contractSpec` was always null server-side and
    // the manager gate approved the run without creating a contract.
    //
    // Spread rather than a nested object, and only when supplied: the server treats
    // a missing spec as "no contract", and omitting the keys entirely is how you
    // express that without sending four nulls.
    ...(contractSpec
      ? {
          freelancer_id: contractSpec.freelancerId,
          total_amount: contractSpec.totalAmount,
          package_id: contractSpec.packageId ?? null,
          project_id: contractSpec.projectId ?? null,
        }
      : {}),
  });
  return data;
};

/**
 * The client gate. Approves a shortlist and invites exactly these freelancers.
 *
 * `freelancerIds` are `FreelancerProfile` ids from the shortlist, not user ids.
 * Duplicates are collapsed server-side, and anyone who cannot be invited comes
 * back in `skipped` rather than failing the whole approval.
 *
 * Sent as `freelancer_ids`. Sent as `freelancerIds` it bound to nothing and the
 * approval arrived with an empty list -- which the service rejected with "Approving
 * an empty shortlist would end the run without contacting anyone", while the page
 * said four people were selected. No invitations were attempted, so nothing was
 * half-sent; but the client's own decision had been discarded on the way out.
 */
export const approveShortlist = async ({ id, freelancerIds, rationale }) => {
  const { data } = await axiosInstance.post(`/ai/workflows/${id}/approve-shortlist`, {
    freelancer_ids: freelancerIds,
    rationale,
  });
  return data;
};

export const resumeWorkflow = async (id) => {
  const { data } = await axiosInstance.post(`/ai/workflows/${id}/resume`, {});
  return data;
};

export const cancelWorkflow = async (id) => {
  const { data } = await axiosInstance.post(`/ai/workflows/${id}/cancel`, {});
  return data;
};

// ── draft ──────────────────────────────────────────────────────────────

/**
 * Ask for help drafting. Returns immediately with a run id; the draft lands
 * later and is fetched with `fetchDraft`.
 *
 * 202, not 200, because the agent is asynchronous relative to the form. A
 * synchronous call would either stall the form while a model thinks or time out,
 * and a client waiting on a spinner for a minute with no feedback is worse than
 * one that can keep typing.
 */
export const requestDraft = async ({ partialText, titleHint, categoryHint, budgetHintUsd, deliveryDaysHint }) => {
  try {
    const { data } = await axiosInstance.post("/ai/draft/draft", {
      partial_text: partialText,
      title_hint: titleHint ?? null,
      category_hint: categoryHint ?? null,
      budget_hint_usd: budgetHintUsd ?? null,
      delivery_days_hint: deliveryDaysHint ?? null,
    });
    return data;
  } catch (error) {
    throw new Error(problemMessage(error, "Could not start a draft."));
  }
};

export const fetchDraft = async (workflowRunId) => {
  const { data } = await axiosInstance.get(`/ai/draft/${workflowRunId}`);
  return data;
};

// ── talent match ───────────────────────────────────────────────────────

/**
 * Start (or re-run) talent matching.
 *
 * `plan` is the client's chosen scoring weights. Omit it and the planner
 * chooses; supply it and the supplied weights are used instead, which is what
 * makes the adjust-and-re-run control on the shortlist page a real control.
 *
 * Sending a plan also supersedes any run already in flight for the project --
 * the server reports which ones in `superseded_workflow_run_ids` so the caller
 * can say so, rather than quietly swapping the list out from under someone who
 * may be reading it.
 */
export const startTalentMatch = async (projectId, plan) => {
  try {
    const { data } = await axiosInstance.post(`/ai/talent-match/start/${projectId}`, {
      plan: plan ?? null,
    });
    return data;
  } catch (error) {
    throw new Error(problemMessage(error, "Could not start talent matching."));
  }
};

export const fetchShortlist = async (workflowRunId) => {
  const { data } = await axiosInstance.get(`/ai/talent-match/${workflowRunId}/shortlist`);
  return data;
};

export const resumeTalentMatch = async (workflowRunId) => {
  const { data } = await axiosInstance.post(`/ai/talent-match/resume/${workflowRunId}`);
  return data;
};
