import { describe, expect, it, vi, beforeEach } from "vitest";
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { MemoryRouter } from "react-router-dom";

// Mocked at the API boundary, so the assertion is about what the client *sends*
// rather than about the shape of a function.
const approveShortlist = vi.hoisted(() => vi.fn());

vi.mock("../aiWorkflowApi", async (importOriginal) => ({
  ...(await importOriginal()),
  approveShortlist,
}));

const { default: ShortlistApprovalPanel } = await import("../ShortlistApprovalPanel");

/**
 * Which ids does approving actually send?
 *
 * ## Why this test exists
 *
 * The panel selected candidates with
 *
 *     new Set(ranked.map((r) => r.freelancerId ?? r.id))
 *
 * There is no camelCase transform on the axios response, so `r.freelancerId` was
 * always `undefined` and the fallback sent each candidate's **recommendation row
 * id** instead of their freelancer profile id.
 *
 * The server stores what arrives as `ShortlistApproval.FreelancerIds` and hands
 * it to the invitation service, which resolves them as freelancer profiles. So
 * approving would have produced zero invitations -- and reported success, because
 * a row id is still a perfectly valid Guid and nothing rejects it.
 *
 * Every other test in this feature asserts what is *rendered*. This one asserts
 * what is *submitted*, which is the only claim that matters here: rendering the
 * right name while sending the wrong id would still invite nobody.
 */

/** Deliberately different values, so a mix-up cannot pass by coincidence. */
const CANDIDATES = [
  {
    // row id 1001, profile id "aaaa..."
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

function renderPanel(shortlist = CANDIDATES) {
  const client = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });

  return render(
    <QueryClientProvider client={client}>
      <MemoryRouter>
        <ShortlistApprovalPanel
          workflowRunId="25"
          status="PendingClientApproval"
          shortlist={shortlist}
        />
      </MemoryRouter>
    </QueryClientProvider>,
  );
}

/** Select everyone, open the confirmation, and confirm. */
async function approveEveryone(user) {
  await user.click(screen.getByRole("checkbox", { name: /select all/i }));
  await user.click(screen.getByRole("button", { name: /^invite/i }));
  await user.click(
    await screen.findByRole("button", { name: /send invitations/i }),
  );
}

describe("what approving sends", () => {
  beforeEach(() => {
    approveShortlist.mockReset();
    approveShortlist.mockResolvedValue({ invitationIds: [1, 2], skipped: [] });
  });

  it("sends the freelancer profile ids, not the recommendation row ids", async () => {
    const user = userEvent.setup();
    renderPanel();

    await approveEveryone(user);

    expect(approveShortlist).toHaveBeenCalledTimes(1);
    const sent = approveShortlist.mock.calls[0][0].freelancerIds;

    expect(sent).toEqual(
      expect.arrayContaining(CANDIDATES.map((c) => c.freelancer_id)),
    );
  });

  it("sends no recommendation row id at all", async () => {
    // The shape of the bug: every value is a valid id, so nothing rejects it.
    const user = userEvent.setup();
    renderPanel();

    await approveEveryone(user);

    const sent = approveShortlist.mock.calls[0][0].freelancerIds;

    for (const candidate of CANDIDATES) {
      expect(sent).not.toContain(candidate.id);
    }
  });

  it("targets the run the client is looking at", async () => {
    const user = userEvent.setup();
    renderPanel();

    await approveEveryone(user);

    expect(approveShortlist.mock.calls[0][0].id).toBe("25");
  });

  it("sends only the candidates the client actually chose", async () => {
    // Select-all then deselect one, so the payload is a subset. An approval that
    // always sent the whole shortlist would ignore the client's decision and
    // contact someone they deliberately excluded.
    const user = userEvent.setup();
    renderPanel();

    await user.click(screen.getByRole("checkbox", { name: /select all/i }));
    await user.click(
      screen.getByRole("checkbox", { name: /invite bravo/i }),
    );
    await user.click(screen.getByRole("button", { name: /^invite/i }));
    await user.click(
      await screen.findByRole("button", { name: /send invitations/i }),
    );

    const sent = approveShortlist.mock.calls[0][0].freelancerIds;

    expect(sent).toEqual([CANDIDATES[0].freelancer_id]);
  });

  it("sends nothing when the client has chosen nobody", async () => {
    // The button is disabled in this state, so the request must not be reachable
    // at all -- an empty approval would end the run without contacting anyone.
    const user = userEvent.setup();
    renderPanel();

    const button = screen.getByRole("button", { name: /^invite/i });
    expect(button).toBeDisabled();

    await user.click(button);
    expect(approveShortlist).not.toHaveBeenCalled();
  });

  it("survives a camelCase payload from a future client", async () => {
    // The component tolerates both casings. Not the shape the server sends
    // today, but the tolerance is deliberate and should not rot silently.
    const user = userEvent.setup();
    renderPanel([
      {
        id: 2001,
        freelancerId: "cccccccc-0000-0000-0000-000000000003",
        freelancerName: "Charlie",
        score: "27.00",
        reason: "Okay fit.",
        strengths: [],
        gaps: [],
      },
    ]);

    await user.click(screen.getByRole("checkbox", { name: /select all/i }));
    await user.click(screen.getByRole("button", { name: /^invite/i }));
    await user.click(
      await screen.findByRole("button", { name: /send invitations/i }),
    );

    expect(approveShortlist.mock.calls[0][0].freelancerIds).toEqual([
      "cccccccc-0000-0000-0000-000000000003",
    ]);
  });
});