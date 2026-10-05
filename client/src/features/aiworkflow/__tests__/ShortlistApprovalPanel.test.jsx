/**
 * The client approval gate, rendered.
 *
 * ## What this is actually protecting
 *
 * `approve-shortlist` sends invitations from the client's account. It is the
 * only AI action with an irreversible, outward-facing consequence, and the two
 * things that can go wrong are:
 *
 *  - offering the button when the run is not awaiting approval, so the client's
 *    only experience is a 400 saying "not awaiting approval"; and
 *  - hiding it when it is, so a client with a finished shortlist has no way to
 *    act on it and the workflow stalls with nobody to blame.
 *
 * Both failures are invisible to a type checker and to a network log. The
 * server refused the first one correctly; the screen is what needs testing.
 */

import React from "react";
import { describe, expect, it, vi, beforeEach } from "vitest";
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";

import ShortlistApprovalPanel from "../ShortlistApprovalPanel";

// The panel talks to react-query and to the toast system. Both are replaced
// with the smallest thing that records what happened, because a test that
// mounts the real query client would be testing axios.
const approveMutateAsync = vi.fn();
const toastSuccess = vi.fn();
const toastError = vi.fn();

vi.mock("../aiWorkflowQueries", () => ({
  useApproveShortlist: () => ({
    mutateAsync: approveMutateAsync,
    isPending: false,
  }),
}));

vi.mock("../../../utils/toast", () => ({
  toastSuccess: (...args) => toastSuccess(...args),
  toastError: (...args) => toastError(...args),
}));

const SHORTLIST = [
  {
    freelancerId: "b944584e-2cd9-43f9-b5c7-a25e2c865ff4",
    score: 87.5,
    reason: "Strong React history with comparable billing work.",
    strengths: ["React", "Stripe"],
    gaps: ["C#"],
  },
  {
    freelancerId: "c2b7f0e1-1111-2222-3333-444455556666",
    score: 61,
    reason: "Solid backend work, weaker on the front end.",
    strengths: ["C#"],
    gaps: ["React"],
  },
];

beforeEach(() => {
  approveMutateAsync.mockReset();
  toastSuccess.mockReset();
  toastError.mockReset();
});

describe("ShortlistApprovalPanel", () => {
  it("offers no usable control when the run is not awaiting approval", () => {
    render(<ShortlistApprovalPanel workflowRunId={7} status="Running" shortlist={SHORTLIST} />);

    // The shortlist is still shown -- it is useful context -- but the select-all
    // control is present and disabled rather than absent, and the per-row
    // checkboxes and the send button are not rendered at all. "Nothing the user
    // can operate" is the guarantee; the select-all bar staying in place keeps
    // the layout from jumping when the run reaches the gate.
    expect(screen.queryByRole("button", { name: /invite/i })).not.toBeInTheDocument();
    expect(screen.getByRole("checkbox", { name: /select all/i })).toBeDisabled();

    // One checkbox total: the disabled select-all. The two per-row ones are
    // gated on `awaiting`, so they are not in the tree.
    expect(screen.getAllByRole("checkbox")).toHaveLength(1);
  });

  it("offers the gate only at PendingClientApproval", () => {
    render(
      <ShortlistApprovalPanel
        workflowRunId={7}
        status="PendingClientApproval"
        shortlist={SHORTLIST}
      />,
    );

    expect(screen.getAllByRole("checkbox").length).toBeGreaterThan(0);
    expect(screen.getByRole("button", { name: /invite/i })).toBeInTheDocument();
  });

  it("does not offer the client gate at the manager gate", async () => {
    // The assertion that most needs to exist. If this gate opened here, a client
    // could send invitations at the point where the platform is waiting for a
    // manager to authorise a contract.
    render(
      <ShortlistApprovalPanel workflowRunId={7} status="PendingManagerApproval" shortlist={SHORTLIST} />,
    );

    expect(screen.queryByRole("button", { name: /invite/i })).not.toBeInTheDocument();
  });

  it("sends exactly the selected people", async () => {
    const user = userEvent.setup();
    render(
      <ShortlistApprovalPanel
        workflowRunId={7}
        status="PendingClientApproval"
        shortlist={SHORTLIST}
      />,
    );

    const boxes = screen.getAllByRole("checkbox");
    // The first checkbox is "select all", so the rows follow it.
    await user.click(boxes[1]);
    await user.click(screen.getByRole("button", { name: /invite 1 person/i }));
    await user.click(await screen.findByRole("button", { name: /send invitations/i }));

    expect(approveMutateAsync).toHaveBeenCalledTimes(1);
    expect(approveMutateAsync).toHaveBeenCalledWith({
      id: 7,
      freelancerIds: [SHORTLIST[0].freelancerId],
    });
  });

  it("will not send an empty selection", () => {
    // The server would reject it, but the control is disabled so nobody has to
    // find out.
    render(
      <ShortlistApprovalPanel
        workflowRunId={7}
        status="PendingClientApproval"
        shortlist={SHORTLIST}
      />,
    );

    expect(screen.getByRole("button", { name: /invite/i })).toBeDisabled();
  });

  it("reports skipped recipients as a warning, not a failure", async () => {
    // A shortlist built from people who applied will contain people who have
    // already proposed, and the server refuses to invite those. Ten invited and
    // two skipped is a good outcome, and the client needs to know before they
    // tell their own client "we have reached out".
    const user = userEvent.setup();
    approveMutateAsync.mockResolvedValue({
      invitationIds: [1, 2, 3],
      skipped: [{ freelancerId: "x", reason: "already applied" }],
    });

    render(
      <ShortlistApprovalPanel
        workflowRunId={7}
        status="PendingClientApproval"
        shortlist={SHORTLIST}
      />,
    );

    await user.click(screen.getByRole("checkbox", { name: /select all/i }));
    await user.click(screen.getByRole("button", { name: /invite 2 people/i }));
    await user.click(await screen.findByRole("button", { name: /send invitations/i }));

    expect(toastSuccess).toHaveBeenCalledWith(expect.stringContaining("already applied"));
    expect(toastError).not.toHaveBeenCalled();
  });

  it("surfaces a server refusal as an error rather than silently doing nothing", async () => {
    const user = userEvent.setup();
    approveMutateAsync.mockRejectedValue(new Error("Only the client who posted this project can approve its shortlist"));

    render(
      <ShortlistApprovalPanel
        workflowRunId={7}
        status="PendingClientApproval"
        shortlist={SHORTLIST}
      />,
    );

    await user.click(screen.getByRole("checkbox", { name: /select all/i }));
    await user.click(screen.getByRole("button", { name: /invite 2 people/i }));
    await user.click(await screen.findByRole("button", { name: /send invitations/i }));

    expect(toastError).toHaveBeenCalledWith(
      expect.stringContaining("can approve its shortlist"),
    );
  });

  it("says nothing has been contacted yet while approval is pending", () => {
    // The single most important fact on the screen at that moment. A client who
    // reads "Working" and assumes invitations went out is worse off than one who
    // was told nothing.
    render(
      <ShortlistApprovalPanel
        workflowRunId={7}
        status="PendingClientApproval"
        shortlist={SHORTLIST}
      />,
    );

    expect(screen.getByText(/nobody has been contacted/i)).toBeInTheDocument();
  });

  it("explains the model's reasoning per candidate", () => {
    render(
      <ShortlistApprovalPanel
        workflowRunId={7}
        status="PendingClientApproval"
        shortlist={SHORTLIST}
      />,
    );

    // Each row carries its own sentence, so a client can weigh two people
    // without opening anything. The score alone is not a reason.
    expect(screen.getByText(SHORTLIST[0].reason)).toBeInTheDocument();
    expect(screen.getByText(SHORTLIST[1].reason)).toBeInTheDocument();

    // Both candidates get a strengths and a gaps section, so the labels appear
    // twice each.
    expect(screen.getAllByText("Strengths")).toHaveLength(2);
    expect(screen.getAllByText("Gaps")).toHaveLength(2);

    // The evidence itself, not just its heading. These are the fields that were
    // being dropped on the way out of the Python service.
    //
    // "React" appearing twice is the interesting part: it is a strength for the
    // first candidate and a gap for the second, and both are on screen. A
    // renderer that collapsed the two lists would show it once and be wrong
    // about one of the two people.
    expect(screen.getAllByText("React")).toHaveLength(2);
    expect(screen.getAllByText("C#")).toHaveLength(2);
    expect(screen.getByText("Stripe")).toBeInTheDocument();
  });

  it("renders an empty shortlist without offering a gate", () => {
    // The case where the model found nobody. There is nothing to approve, and a
    // live button over an empty list would be a dead end.
    render(<ShortlistApprovalPanel workflowRunId={7} status="PendingClientApproval" shortlist={[]} />);

    expect(screen.queryByRole("button", { name: /send invitations/i })).not.toBeInTheDocument();
    expect(screen.getByText(/no suggestions yet/i)).toBeInTheDocument();
  });
});
