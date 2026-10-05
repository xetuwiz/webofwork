import { describe, expect, it, vi, beforeEach } from "vitest";
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { MemoryRouter } from "react-router-dom";

const approveShortlist = vi.hoisted(() => vi.fn());
const toastSuccess = vi.hoisted(() => vi.fn());

vi.mock("../aiWorkflowApi", async (importOriginal) => ({
  ...(await importOriginal()),
  approveShortlist,
}));

vi.mock("../../../utils/toast", () => ({
  toastSuccess,
  toastError: vi.fn(),
}));

const { default: ShortlistApprovalPanel } = await import("../ShortlistApprovalPanel");

/**
 * Does the client report what actually happened?
 *
 * ## The failure this exists for
 *
 * After approving a shortlist, the panel said **"Invited 0 people"** — while the
 * invitations were being created.
 *
 *     // AiWorkflowController.cs
 *     invitation_ids = result.InvitationIds,
 *
 * The wire key is `invitation_ids`. The panel read `result?.invitationIds?.length`,
 * so it was `undefined`, `?? 0` swallowed it, and the screen confidently reported
 * zero. There is no camelCase transform on the axios response to bridge the two:
 * `Program.cs` sets `PropertyNamingPolicy = SnakeCaseLower`, and a C#
 * `ApprovalDecision` likewise arrives as `approval_decision`, which is why the same
 * mistake hid the recorded decision on three other screens.
 *
 * ## Why this fixture is snake_case
 *
 * Because that is what the server sends. A camelCase fixture would pass against the
 * broken code, which is the trap: the tests looked reasonable and the screen was
 * wrong.
 */

const CANDIDATES = [
  {
    id: 1001,
    freelancer_id: "aaaaaaaa-0000-0000-0000-000000000001",
    freelancer_name: "Alpha",
    score: "29.50",
    reason: "Strong fit.",
    strengths: [],
    gaps: [],
  },
  {
    id: 1002,
    freelancer_id: "bbbbbbbb-0000-0000-0000-000000000002",
    freelancer_name: "Bravo",
    score: "28.20",
    reason: "Good fit.",
    strengths: [],
    gaps: [],
  },
];

function renderPanel() {
  const client = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });

  return render(
    <QueryClientProvider client={client}>
      <MemoryRouter>
        <ShortlistApprovalPanel
          workflowRunId="25"
          status="PendingClientApproval"
          shortlist={CANDIDATES}
        />
      </MemoryRouter>
    </QueryClientProvider>,
  );
}

async function approveEveryone(user) {
  await user.click(screen.getByRole("checkbox", { name: /select all/i }));
  await user.click(screen.getByRole("button", { name: /^invite/i }));
  await user.click(await screen.findByRole("button", { name: /send invitations/i }));
}

describe("what the approval reports", () => {
  beforeEach(() => {
    approveShortlist.mockReset();
    toastSuccess.mockReset();
  });

  it("counts the invitations the server says it created", async () => {
    // The exact response shape from the controller.
    approveShortlist.mockResolvedValue({
      invitation_ids: [501, 502],
      skipped: [],
    });

    const user = userEvent.setup();
    renderPanel();
    await approveEveryone(user);

    expect(toastSuccess).toHaveBeenCalledWith(expect.stringContaining("Invited 2"));
  });

  it("never claims zero invitations when the server created some", async () => {
    approveShortlist.mockResolvedValue({
      invitation_ids: [501, 502, 503],
      skipped: [],
    });

    const user = userEvent.setup();
    renderPanel();
    await approveEveryone(user);

    expect(toastSuccess).not.toHaveBeenCalledWith(
      expect.stringContaining("Invited 0"),
    );
  });

  it("still tolerates a camelCase response from a future server", async () => {
    // Deliberate tolerance. If the naming policy is ever changed, this keeps
    // working rather than silently reverting to reporting zero.
    approveShortlist.mockResolvedValue({
      invitationIds: [501],
      skipped: [],
    });

    const user = userEvent.setup();
    renderPanel();
    await approveEveryone(user);

    expect(toastSuccess).toHaveBeenCalledWith(expect.stringContaining("Invited 1"));
  });

  it("reports zero only when the server really created none", async () => {
    approveShortlist.mockResolvedValue({ invitation_ids: [], skipped: [] });

    const user = userEvent.setup();
    renderPanel();
    await approveEveryone(user);

    expect(toastSuccess).toHaveBeenCalledWith(expect.stringContaining("Invited 0"));
  });

  it("still names the people it could not invite", async () => {
    approveShortlist.mockResolvedValue({
      invitation_ids: [501],
      skipped: [{ freelancer_id: "bbbbbbbb-0000-0000-0000-000000000002", reason: "Already applied" }],
    });

    const user = userEvent.setup();
    renderPanel();
    await approveEveryone(user);

    const message = toastSuccess.mock.calls[0][0];
    expect(message).toContain("Invited 1");
    expect(message).toContain("Already applied");
  });

  it("does not claim a success it was not told about", async () => {
    // A response with neither spelling. Reporting zero would be a guess; the
    // panel's contract is to reflect the server, so this asserts the count is not
    // fabricated from the number of ticked rows.
    approveShortlist.mockResolvedValue({ skipped: [] });

    const user = userEvent.setup();
    renderPanel();
    await approveEveryone(user);

    // Two rows were ticked, so "Invited 2" here would mean the count came from
    // the selection rather than from the server.
    expect(toastSuccess).not.toHaveBeenCalledWith(
      expect.stringContaining("Invited 2"),
    );
  });
});