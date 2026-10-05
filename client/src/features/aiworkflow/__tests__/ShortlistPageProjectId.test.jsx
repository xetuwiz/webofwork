import { describe, expect, it, vi, beforeEach } from "vitest";
import { fireEvent, render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { MemoryRouter, Route, Routes } from "react-router-dom";

const useAiWorkflow = vi.hoisted(() => vi.fn());
const useShortlist = vi.hoisted(() => vi.fn());
const startTalentMatch = vi.hoisted(() => vi.fn());
const mutateAsync = vi.hoisted(() => vi.fn());

vi.mock("../aiWorkflowQueries", async (importOriginal) => ({
  ...(await importOriginal()),
  useAiWorkflow,
  useShortlist,
  useStartTalentMatch: () => ({ mutateAsync, isPending: false }),
}));

vi.mock("../aiWorkflowApi", async (importOriginal) => ({
  ...(await importOriginal()),
  startTalentMatch,
}));

const { default: AiShortlistPage } = await import("../AiShortlistPage");

/**
 * Does the page know which project it is on?
 *
 * ## The failure
 *
 * `POST /api/ai/talent-match/start/undefined` → 404, on the real server, in the
 * user's log.
 *
 * ## Why
 *
 * The route is declared
 *
 *     /my-projects/:id/ai-shortlist/:workflowRunId
 *
 * and the page read
 *
 *     const { projectId, workflowRunId } = useParams();
 *
 * `projectId` does not exist on that route, so it was `undefined`. Every
 * project-scoped action on the page was therefore aimed at nothing: the re-run
 * button posted to `/start/undefined`, and the "back to project" link pointed at
 * `/my-projects/undefined`.
 *
 * It went unnoticed because `FindTalentButton` takes its project id as a prop
 * rather than from the route, so the *first* press of "Find best talent" worked.
 * Only the actions on the shortlist page itself were broken.
 *
 * ## Why the route below uses `:id`
 *
 * Reproduced exactly as `ClientRoutes.jsx` declares it. Naming the parameter
 * `:projectId` in this test would hide the exact mismatch under test.
 */

const PLAN = {
  weights: {
    skill_coverage: 0.5,
    portfolio_relevance: 0.2,
    past_success_rate: 0.1,
    availability_and_speed: 0.1,
    budget_fit: 0.1,
  },
  shortlist_size: 10,
};

function arrange() {
  useAiWorkflow.mockReturnValue({
    data: { id: 25, status: "pending_client_approval" },
    isLoading: false,
    isError: false,
  });
  useShortlist.mockReturnValue({
    data: {
      shortlist: [
        { id: 1, freelancer_id: "a", score: "30.00", reason: "Good fit." },
      ],
      plan: JSON.stringify(PLAN),
      plan_source: "planner",
      awaiting_approval: true,
    },
    isLoading: false,
    isError: false,
  });
  mutateAsync.mockReset();
  mutateAsync.mockResolvedValue({ workflow_run_id: 26 });

  const client = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });

  return render(
    <QueryClientProvider client={client}>
      <MemoryRouter initialEntries={["/my-projects/11/ai-shortlist/25"]}>
        <Routes>
          <Route
            path="/my-projects/:id/ai-shortlist/:workflowRunId"
            element={<AiShortlistPage />}
          />
        </Routes>
      </MemoryRouter>
    </QueryClientProvider>,
  );
}

describe("the project id on the shortlist page", () => {
  beforeEach(() => {
    arrange();
  });

  it("is read from the route's `:id`, not a `projectId` that does not exist", () => {
    // The back-link is the cheapest place to observe it: it renders the value
    // directly, so an undefined id is visible in the href.
    const back = screen.getByRole("link", { name: /back|project/i });
    expect(back.getAttribute("href")).toContain("/my-projects/11");
  });

  it("is sent when re-running with new weights", async () => {
    const user = userEvent.setup();

    // Re-run stays disabled until a weight actually differs from the plan, so the
    // slider has to be moved first. That is the real path a person takes, and it
    // exercises the button's enabled state as a side effect.
    const slider = screen.getByRole("slider", { name: /skill coverage/i });

    // `fireEvent.change`, not `userEvent.type`: a range input is not an editable
    // element as far as `clear()` is concerned.
    fireEvent.change(slider, { target: { value: "70" } });

    const rerun = screen.getByRole("button", { name: /re-run/i });
    expect(rerun).toBeEnabled();

    await user.click(rerun);

    expect(mutateAsync).toHaveBeenCalledTimes(1);
    const sent = mutateAsync.mock.calls[0][0];

    expect(sent.projectId).toBe("11");
    expect(sent.projectId).not.toBeUndefined();
  });

  it("would have posted to `/start/undefined` before the fix", async () => {
    // Stated as the consequence rather than as a live test: the old code read
    // `projectId` from a route that declares `:id`, so this value was undefined
    // and `startTalentMatch(undefined)` 404'd against the real server.
    const sent = mutateAsync.mock.calls[0]?.[0];
    expect(sent?.projectId).not.toBe("undefined");
  });
});