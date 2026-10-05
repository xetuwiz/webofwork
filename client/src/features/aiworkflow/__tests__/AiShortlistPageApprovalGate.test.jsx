import { describe, expect, it } from "vitest";
import { render, screen } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { MemoryRouter, Route, Routes } from "react-router-dom";

import AiShortlistPage from "../AiShortlistPage";

/**
 * Can a client act on a shortlist the AI has finished checking?
 *
 * ## The question this answers
 *
 * Run 25 was `pending_client_approval` with four scored candidates on screen, and
 * the page offered no way to approve them. The approval control is the entire
 * point of the gate -- the client decides who gets contacted -- and it was
 * unreachable.
 *
 * ## Why it was missing
 *
 * The server serialises the status as `pending_client_approval`
 * (`JsonStringEnumConverter(SnakeCaseLower)`, registered globally). The page
 * asked `canApproveShortlist(status)`, which compared against the C# spelling
 * `PendingClientApproval`. Permanently false, no error, no Approve button.
 *
 * Rendered rather than unit-tested because the failure was a *missing control*:
 * asserting on `canApproveShortlist` alone would have passed while the button
 * still failed to render, which is the mistake that let this ship.
 */

const CANDIDATE = {
  id: 1,
  freelancer_id: "09faf944-505c-4ae7-9a89-a78261d7d85e",
  score: "30.00",
  reason: "Solid portfolio.",
  strengths: ["React"],
  gaps: ["Firebase"],
  created_at: "2026-10-04T20:59:05Z",
};

/** What `useAiWorkflow` and `useShortlist` resolve to for a finished run. */
function arrange({ status, plan = null, planSource = null, shortlist = [CANDIDATE] }) {
  const run = {
    id: 25,
    project_id: 11,
    stage: "talent_match",
    status,
    objective: "Find talent",
    approval_decision: null,
    approval_rationale: null,
  };

  const list = {
    workflow_run_id: 25,
    stage: "talent_match",
    status,
    plan,
    plan_source: planSource,
    awaiting_approval: status === "PendingClientApproval",
    shortlist,
  };

  // Keyed by the *string* ids `useParams` yields. Numbering them misses the cache,
  // the hooks fall through to a real fetch, and the page renders "Checking
  // progress" -- which fails every assertion below for a reason unrelated to the
  // status casing being tested.
  const client = new QueryClient({
    defaultOptions: { queries: { retry: false, gcTime: 0 } },
  });
  client.setQueryData(["ai-workflow", "25"], run);
  client.setQueryData(["ai-workflow", "project", "11"], run);
  client.setQueryData(["ai-shortlist", "25"], list);

  // The page takes no props -- it reads `useParams` -- so it has to be mounted at
  // a matching route. Passing props renders an empty page and every assertion
  // below fails for a reason that has nothing to do with the status casing.
  return render(
    <QueryClientProvider client={client}>
      <MemoryRouter initialEntries={["/my-projects/11/ai-shortlist/25"]}>
        <Routes>
          <Route
            path="/my-projects/:projectId/ai-shortlist/:workflowRunId"
            element={<AiShortlistPage />}
          />
        </Routes>
      </MemoryRouter>
    </QueryClientProvider>,
  );
}

describe("AiShortlistPage at the approval gate", () => {
  it("offers approval for the status the server actually sends", () => {
    arrange({ status: "pending_client_approval" });

    // The assertion this whole file exists for. The control reads
    // "Invite N people"; "Send invitations" is on the confirmation dialog inside it.
    expect(
      screen.getByRole("button", { name: /^invite/i }),
    ).toBeInTheDocument();
  });

  it("offers the control but keeps it disabled until someone is chosen", () => {
    // Nobody is selected on arrival, and inviting zero people is not a thing the
    // client should be able to do by clicking a stale button.
    arrange({ status: "pending_client_approval" });

    expect(
      screen.getByRole("button", { name: /^invite/i }),
    ).toBeDisabled();
  });

  it("offers approval for the C# spelling too", () => {
    arrange({ status: "PendingClientApproval" });

    expect(
      screen.getByRole("button", { name: /^invite/i }),
    ).toBeInTheDocument();
  });

  it("shows the shortlist it is asking about", () => {
    arrange({ status: "pending_client_approval" });

    expect(screen.getAllByText(/solid portfolio/i).length).toBeGreaterThan(0);
    expect(screen.getAllByText(/firebase/i).length).toBeGreaterThan(0);
  });

  it("says nobody has been contacted yet", () => {
    // The reassurance that makes approving a real decision rather than a guess.
    arrange({ status: "pending_client_approval" });

    expect(screen.getAllByText(/nobody has been contacted/i).length).toBeGreaterThan(0);
  });

  it("does not offer approval while the AI is still working", () => {
    arrange({ status: "running", shortlist: [] });

    expect(
      screen.queryByRole("button", { name: /^invite/i }),
    ).toBeNull();
  });

  it("does not offer approval for an empty shortlist", () => {
    // Nothing to approve. Enabling the control here would send zero invitations
    // and report success.
    arrange({ status: "pending_client_approval", shortlist: [] });

    expect(
      screen.queryByRole("button", { name: /^invite/i }),
    ).toBeNull();
  });

  it("stops offering approval once it has been given", () => {
    arrange({ status: "approved" });

    expect(
      screen.queryByRole("button", { name: /^invite/i }),
    ).toBeNull();
  });

  it("never claims the page is out of date", () => {
    // That sentence came from the unrecognised-status fallback, on a page that
    // was perfectly current.
    arrange({ status: "pending_client_approval" });

    expect(screen.queryByText(/newer than this page/i)).toBeNull();
  });

  it("labels the state in words rather than as an enum name", () => {
    arrange({ status: "pending_client_approval" });

    expect(screen.getAllByText(/needs your approval/i).length).toBeGreaterThan(0);
    expect(screen.queryByText(/pending_client_approval/)).toBeNull();
  });

  it("explains a failure instead of looking like it is still working", () => {
    arrange({ status: "failed", shortlist: [] });

    expect(screen.getAllByText(/nothing was contacted/i).length).toBeGreaterThan(0);
  });
});