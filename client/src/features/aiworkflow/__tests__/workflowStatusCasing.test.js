import { describe, expect, it } from "vitest";

import {
  canApproveContract,
  isTerminalStatus,
  canApproveShortlist,
  describeStatus,
  normaliseWorkflowStatus,
} from "../workflowDisplay";

/**
 * Does the page understand the statuses the server actually sends?
 *
 * ## The failure this exists for
 *
 * `Program.cs` registers `JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower)`
 * globally, so a run's status arrives as `pending_client_approval`. Every
 * comparison in this feature was written against the C# spelling,
 * `PendingClientApproval`, and nothing reconciled the two.
 *
 * Observed on run 25, a finished shortlist awaiting approval:
 *
 *   heading  pending_client_approval
 *   blurb    "This state is newer than this page."
 *   buttons  no Approve control at all
 *
 * The blurb was never about staleness. `describeStatus` missed its lookup table
 * and fell through to a default whose wording was about the page rather than the
 * status, which is why a phrase about the page kept appearing on a page that was
 * perfectly current.
 *
 * The missing Approve button is the serious half. `canApproveShortlist` was
 * permanently false, so the approval flow -- the entire point of the gate -- was
 * unreachable. The client had a shortlist it could look at and no way to act.
 */

/** What the wire actually sends, from the global enum converter. */
const WIRE = {
  pending: "pending",
  running: "running",
  pending_approval: "pending_approval",
  pending_client_approval: "pending_client_approval",
  pending_manager_approval: "pending_manager_approval",
  validation_failed: "validation_failed",
  approved: "approved",
  rejected: "rejected",
  completed: "completed",
  failed: "failed",
  cancelled: "cancelled",
};

describe("normaliseWorkflowStatus", () => {
  it("maps every status the server sends to the C# spelling", () => {
    expect(normaliseWorkflowStatus(WIRE.pending_client_approval)).toBe(
      "PendingClientApproval",
    );
    expect(normaliseWorkflowStatus(WIRE.pending_manager_approval)).toBe(
      "PendingManagerApproval",
    );
    expect(normaliseWorkflowStatus(WIRE.pending_approval)).toBe("PendingApproval");
    expect(normaliseWorkflowStatus(WIRE.validation_failed)).toBe("ValidationFailed");
    expect(normaliseWorkflowStatus(WIRE.completed)).toBe("Completed");
    expect(normaliseWorkflowStatus(WIRE.failed)).toBe("Failed");
  });

  it("leaves an already-correct spelling alone", () => {
    // Checked before transforming, so a round trip cannot mangle it.
    expect(normaliseWorkflowStatus("PendingClientApproval")).toBe(
      "PendingClientApproval",
    );
  });

  it("is idempotent", () => {
    const once = normaliseWorkflowStatus(WIRE.pending_client_approval);
    expect(normaliseWorkflowStatus(once)).toBe(once);
  });

  it("returns an unrecognised status untouched rather than blanking it", () => {
    // A status added in C# after this build must still be *displayed*. Turning it
    // into null would hide the run.
    expect(normaliseWorkflowStatus("awaiting_legal_review")).toBe(
      "awaiting_legal_review",
    );
  });

  it("passes through what is not a string", () => {
    expect(normaliseWorkflowStatus(undefined)).toBeUndefined();
    expect(normaliseWorkflowStatus(null)).toBeNull();
  });
});

describe("canApproveShortlist", () => {
  it("is true for the status the server actually sends", () => {
    // The assertion the whole bug turns on.
    expect(canApproveShortlist(WIRE.pending_client_approval)).toBe(true);
  });

  it("is still true for the C# spelling", () => {
    expect(canApproveShortlist("PendingClientApproval")).toBe(true);
  });

  it("is false for a run still working", () => {
    expect(canApproveShortlist(WIRE.running)).toBe(false);
    expect(canApproveShortlist(WIRE.pending)).toBe(false);
  });

  it("is false once approved", () => {
    expect(canApproveShortlist(WIRE.approved)).toBe(false);
  });

  it("is false for the manager gate, which is not the client's to approve", () => {
    expect(canApproveShortlist(WIRE.pending_manager_approval)).toBe(false);
  });
});

describe("canApproveContract", () => {
  it("accepts both gates the manager can act on", () => {
    expect(canApproveContract(WIRE.pending_manager_approval)).toBe(true);
    expect(canApproveContract(WIRE.pending_approval)).toBe(true);
  });

  it("rejects the client gate", () => {
    expect(canApproveContract(WIRE.pending_client_approval)).toBe(false);
  });
});

describe("describeStatus", () => {
  it("gives the approval gate real copy rather than a fallback", () => {
    const { label, blurb, tone } = describeStatus(WIRE.pending_client_approval);

    expect(label).toBe("Needs your approval");
    expect(tone).toBe("warn");
    expect(blurb).toBe("A shortlist is ready. Nobody has been contacted yet.");
  });

  it("never claims the page is stale", () => {
    // The sentence that kept appearing on a current page. Asserted across every
    // status the server sends, because it came from the shared fallback.
    for (const wire of Object.values(WIRE)) {
      expect(describeStatus(wire).blurb).not.toMatch(/newer than this page/i);
    }
  });

  it("shows a failure as a failure", () => {
    const { label, tone } = describeStatus(WIRE.failed);
    expect(label).toBe("Failed");
    expect(tone).toBe("danger");
  });

  it("still displays a status it has no copy for", () => {
    const { label } = describeStatus("awaiting_legal_review");
    expect(label).toBe("awaiting_legal_review");
  });

  it("reports an absent status as unknown", () => {
    expect(describeStatus(undefined).label).toBe("Unknown");
  });
});

describe("terminal statuses", () => {
  it("recognises the wire spellings, so polling stops", () => {
    // Every consumer asks isTerminalStatus, so it has to answer for whatever the
    // wire sends. A direct set lookup missed these and a finished run was polled
    // forever.
    for (const wire of ["completed", "failed", "cancelled", "rejected"]) {
      expect(isTerminalStatus(wire)).toBe(true);
    }
  });

  it("recognises the C# spellings too", () => {
    for (const csharp of ["Completed", "Failed", "Cancelled", "Rejected"]) {
      expect(isTerminalStatus(csharp)).toBe(true);
    }
  });

  it("is false while a run is still moving or waiting on a person", () => {
    // "Waiting" is not "finished". Treating the approval gate as terminal is how a
    // page stops updating while the user is still deciding.
    for (const wire of ["pending", "running", "pending_client_approval"]) {
      expect(isTerminalStatus(wire)).toBe(false);
    }
  });

  it("is false for a status nobody has heard of", () => {
    // Polling an unknown status is the safe default: it costs a request, whereas
    // stopping early hides a run that is still moving.
    expect(isTerminalStatus("awaiting_legal_review")).toBe(false);
    expect(isTerminalStatus(undefined)).toBe(false);
  });
});