import { describe, expect, it } from "vitest";

import {
  describeStatus,
  isActivelyRunning,
  isTerminalStatus,
  normaliseWorkflowStatus,
} from "../workflowDisplay";

/**
 * Regression tests for two spinners that never stopped.
 *
 * Both came from asking "is this finished?" when the question was "is this
 * moving?". A run that is waiting for a person has not finished and is never
 * going to, so a spinner keyed on unfinished animated for as long as the page
 * was open -- next to the approve button the user was being asked to press.
 */
describe("isActivelyRunning", () => {
  it("is true only while the run is moving on its own", () => {
    expect(isActivelyRunning("Pending")).toBe(true);
    expect(isActivelyRunning("Running")).toBe(true);
  });

  it.each(["pending", "running"])("reads the snake_case spelling from the wire: %s", (status) => {
    // The server serialises every enum snake_case. A direct comparison against
    // the C# spelling silently returns false, which is how an unknown status came
    // to be treated as "not running" and the spinner came back.
    expect(isActivelyRunning(status)).toBe(true);
  });

  it("is false at every human gate", () => {
    // Each of these is a page that says "you have something to do". Spinning
    // through all three told the client the server was busy while the only thing
    // outstanding was them.
    expect(isActivelyRunning("PendingClientApproval")).toBe(false);
    expect(isActivelyRunning("PendingManagerApproval")).toBe(false);
    expect(isActivelyRunning("PendingApproval")).toBe(false);
  });

  it("is false at a dead end that is not terminal", () => {
    // ValidationFailed is a stop, not a wait. isTerminalStatus is false for it,
    // which is precisely why the old "not done" check kept spinning.
    expect(isTerminalStatus("ValidationFailed")).toBe(false);
    expect(isActivelyRunning("ValidationFailed")).toBe(false);
  });

  it("is false once a run has stopped", () => {
    for (const status of ["Completed", "Failed", "Cancelled", "Rejected", "Approved"]) {
      expect(isActivelyRunning(status)).toBe(false);
    }
  });

  it("does not assume an unknown status is running", () => {
    // A status added in C# after this shipped must not be polled or animated.
    // Guessing "running" is how a new enum member turns into a spinner that
    // outlives the release that introduced it.
    expect(isActivelyRunning("AwaitingCarrierPigeon")).toBe(false);
    expect(isActivelyRunning(undefined)).toBe(false);
    expect(isActivelyRunning(null)).toBe(false);
  });
});

describe("the human gates are not terminal but are not running either", () => {
  it.each([
    ["PendingClientApproval", "Needs your approval"],
    ["PendingManagerApproval", "Awaiting manager approval"],
  ])("%s reads as %s", (status, label) => {
    // The gap that produced the bug, stated as an assertion so that closing it
    // accidentally is a failing test rather than a stuck spinner.
    expect(isTerminalStatus(status)).toBe(false);
    expect(isActivelyRunning(status)).toBe(false);
    expect(describeStatus(status).label).toBe(label);
  });
});

describe("normaliseWorkflowStatus", () => {
  it("settles snake_case onto the C# spelling", () => {
    expect(normaliseWorkflowStatus("pending_client_approval")).toBe("PendingClientApproval");
    expect(normaliseWorkflowStatus("validation_failed")).toBe("ValidationFailed");
  });

  it("passes an unrecognised status through rather than blanking it", () => {
    // Showing the raw string is how a new server status stays diagnosable: an
    // unknown status returns `raw` unchanged rather than the PascalCase guess,
    // because a guess that misses STATUS_COPY is the same string either way, and
    // a guess that accidentally hits is a status nobody wrote.
    expect(normaliseWorkflowStatus("awaiting_carrier_pigeon")).toBe("awaiting_carrier_pigeon");
    expect(describeStatus("awaiting_carrier_pigeon").label).toBe("awaiting_carrier_pigeon");
  });
});
