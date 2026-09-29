import { describe, expect, it, vi } from "vitest";
import { render, screen } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { MemoryRouter, Route, Routes } from "react-router-dom";

import AiShortlistPage from "../AiShortlistPage";

/**
 * Does the page show what the agent said?
 *
 * ## The two failures this exists for
 *
 * 1. **The clarification question was dropped.** The planner and the drafter both
 *    stop to ask the client one specific question, and it lands in the run's
 *    `final_output`. But the projection the page polls
 *    (`AiWorkflowController.GetProjectWorkflowStatus` and the by-id sibling)
 *    selected `Id, ProjectId, Status, Objective, ApprovalDecision,
 *    ApprovalRationale, CreatedAt, UpdatedAt` -- and not `FinalOutput`. So the
 *    question could not reach the browser by any route. The page said it was
 *    waiting and offered nothing to answer, and the run could not be moved forward
 *    from the screen at all.
 *
 * 2. **The "No matches found" panel could never render.** It read
 *    `shortlist.data.noMatchReason`; the endpoint emitted no such key, and when it
 *    does now it emits `no_match_reason`. Two independent reasons for one dead
 *    branch.
 *
 * ## Why the fixtures are snake_case
 *
 * `Program.cs` sets `PropertyNamingPolicy = SnakeCaseLower` and there is no
 * camelCase transform on the axios response. A camelCase fixture here would pass
 * against the broken code -- which is exactly the trap the previous version of
 * this page fell into, repeatedly.
 */

/**
 * `AiShortlistPage` takes no props; it reads the route. Both hooks are stubbed by
 * module-mocking the query module, which is where the wire shape enters.
 */
const useAiWorkflow = vi.hoisted(() => vi.fn());
const useShortlist = vi.hoisted(() => vi.fn());
const useStartTalentMatch = vi.hoisted(() => vi.fn());

vi.mock("../aiWorkflowQueries", async (importOriginal) => ({
  ...(await importOriginal()),
  useAiWorkflow,
  useShortlist,
  useStartTalentMatch,
}));

const loaded = (data) => ({ data, isLoading: false, isError: false });

function arrange({ run, shortlist }) {
  useAiWorkflow.mockReturnValue(loaded(run));
  useShortlist.mockReturnValue(loaded(shortlist));
  useStartTalentMatch.mockReturnValue({ mutateAsync: vi.fn(), isPending: false });

  const client = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });

  return render(
    <QueryClientProvider client={client}>
      <MemoryRouter initialEntries={["/my-projects/11/ai-shortlist/25"]}>
        <Routes>
          {/* The real route uses `:id`, not `:projectId`. Reproduced exactly:
              naming it differently here would have hidden the mismatch that made
              every rerun post to `/start/undefined`. */}
          <Route path="/my-projects/:id/ai-shortlist/:workflowRunId" element={<AiShortlistPage />} />
        </Routes>
      </MemoryRouter>
    </QueryClientProvider>,
  );
}

describe("the agent's clarifying question", () => {
  it("is shown when the run stopped to ask one", () => {
    arrange({
      run: {
        id: 25,
        status: "pending_approval",
        final_output: JSON.stringify({
          clarification_question: "What is your target budget for the mobile app?",
        }),
      },
      shortlist: { shortlist: [], plan: null, plan_source: null, awaiting_approval: false },
    });

    expect(
      screen.getByText("What is your target budget for the mobile app?"),
    ).toBeInTheDocument();
  });

  it("is read from an already-parsed payload too", () => {
    // `final_output` is jsonb stored as text, so a string is the expected shape.
    // A parsed object is tolerated rather than assumed away.
    arrange({
      run: {
        id: 25,
        status: "pending_approval",
        final_output: { clarification_question: "Which platform first?" },
      },
      shortlist: { shortlist: [], plan: null, plan_source: null, awaiting_approval: false },
    });

    expect(screen.getByText("Which platform first?")).toBeInTheDocument();
  });

  it("does not appear when there is no question", () => {
    arrange({
      run: {
        id: 25,
        status: "pending_client_approval",
        final_output: JSON.stringify({ plan: { weights: {} } }),
      },
      shortlist: {
        shortlist: [{ id: 1, freelancer_id: "a", score: "30.00", reason: "Good." }],
        plan: null,
        plan_source: "planner",
        awaiting_approval: true,
      },
    });

    expect(screen.queryByText(/needs one more thing/i)).toBeNull();
  });

  it("does not break the page when the payload will not parse", () => {
    // The payload is supplementary. Failing the page over it would hide the
    // shortlist the client came for.
    arrange({
      run: { id: 25, status: "pending_client_approval", final_output: "{broken" },
      shortlist: {
        shortlist: [{ id: 1, freelancer_id: "a", score: "30.00", reason: "Still here." }],
        plan: null,
        plan_source: "planner",
        awaiting_approval: true,
      },
    });

    expect(screen.getByText("Still here.")).toBeInTheDocument();
  });

  it("tells the client the answer is not collected on this page", () => {
    // Otherwise the question reads as an input field that failed to render.
    arrange({
      run: {
        id: 25,
        status: "pending_approval",
        final_output: JSON.stringify({ clarification_question: "How many pages?" }),
      },
      shortlist: { shortlist: [], plan: null, plan_source: null, awaiting_approval: false },
    });

    expect(screen.getByText(/not read from this page/i)).toBeInTheDocument();
  });
});

describe("the empty-shortlist explanation", () => {
  const emptyShortlist = {
    shortlist: [],
    plan: null,
    plan_source: "planner",
    awaiting_approval: false,
  };

  it("is shown when the server sends it", () => {
    arrange({
      run: { id: 25, status: "completed" },
      shortlist: {
        ...emptyShortlist,
        no_match_reason: "The AI scored the pool and nobody scored high enough.",
      },
    });

    expect(
      screen.getByText("The AI scored the pool and nobody scored high enough."),
    ).toBeInTheDocument();
  });

  it("is not shown while the run is still working", () => {
    // The server returns null here: "no matches" before anyone has looked would be
    // a claim nobody has made, and it would flicker away on the next poll.
    arrange({
      run: { id: 25, status: "running" },
      shortlist: { ...emptyShortlist, no_match_reason: null },
    });

    expect(screen.queryByText(/no matches found/i)).toBeNull();
  });

  it("reads the snake_case key the API actually sends", () => {
    // It used to read `noMatchReason`, which the endpoint never emitted -- so even
    // once the server started sending a reason, the panel stayed dead.
    arrange({
      run: { id: 25, status: "completed" },
      shortlist: { ...emptyShortlist, no_match_reason: "Nobody scored high enough." },
    });

    expect(screen.getByText("No matches found")).toBeInTheDocument();
  });
});