/**
 * The two approval gates, as the client sees them.
 *
 * ## Why these are pure functions
 *
 * `workflowDisplay.js` decides three things: what a run is called, whether the
 * approve control should exist, and whether the page should keep polling. All
 * three are load-bearing and none of them involve React.
 *
 * Getting any of them wrong is expensive and hard to see. A status mapped to the
 * wrong word tells a client their shortlist is "Cancelled" while the server is
 * waiting for their approval -- and nothing in the network tab shows a problem,
 * because the response was read correctly and rendered wrongly. Polling a run
 * that is waiting on a human burns a request every few seconds for as long as
 * they take to read it.
 *
 * So they are tested here, where a failure names the rule rather than a
 * timeout.
 */

import { describe, expect, it } from "vitest";

import {
  TERMINAL_STATUSES,
  canApproveContract,
  canApproveShortlist,
  describeStatus,
  formatScore,
  hasShortlist,
  shortlistKey,
} from "../workflowDisplay";
import { isWaitingOnClient, isWaitingOnManager, pollIntervalFor } from "../aiWorkflowQueries";

/**
 * The status values the server sends.
 *
 * `WorkflowRunStatus` serialises through `JsonStringEnumConverter`, so the names
 * are the PascalCase enum members verbatim. Listing them literally is the point:
 * a rename in C# must break this test rather than silently turn a gate into
 * dead UI.
 */
const SERVER_STATUSES = [
  "Pending",
  "Running",
  "PendingApproval",
  "PendingClientApproval",
  "PendingManagerApproval",
  "ValidationFailed",
  "Approved",
  "Rejected",
  "Completed",
  "Failed",
  "Cancelled",
];

describe("describeStatus", () => {
  it("never shows a multi-word enum name to a client", () => {
    // "PendingClientApproval" as a label tells a client nothing, and it is
    // visible in every state a run passes through.
    //
    // Single words are exempt: "Approved", "Rejected", "Failed" and "Cancelled"
    // are ordinary English, and rewriting them to avoid matching their own enum
    // name would make the copy worse. So the test looks for the actual defect --
    // a PascalCase run with no spaces -- rather than for equality with the enum.
    for (const status of SERVER_STATUSES) {
      const { label } = describeStatus(status);
      expect(label, `"${status}" is shown as a raw enum name`).not.toMatch(/\b[A-Z][a-z]+[A-Z]/);
      expect(label.trim(), `"${status}" has no label at all`).not.toBe("");
    }
  });

  it("names the two approval gates in the reviewer's own words", () => {
    // These are the two states where somebody is being asked to make a decision.
    // "PendingClientApproval" tells a client nothing; "Needs your approval" tells
    // them what is expected of them.
    expect(describeStatus("PendingClientApproval").label).toBe("Needs your approval");
    expect(describeStatus("PendingManagerApproval").label).toBe("Awaiting manager approval");
  });

  it("distinguishes the two gates", () => {
    // They are different decisions with different consequences -- one sends
    // invitations, the other authorises a contract. A UI that showed the same
    // copy for both would make them indistinguishable.
    const client = describeStatus("PendingClientApproval");
    const manager = describeStatus("PendingManagerApproval");
    expect(client.label).not.toBe(manager.label);
    expect(client.blurb).not.toBe(manager.blurb);
  });

  it("says nothing has been sent when a shortlist awaits approval", () => {
    // The single most important fact at that moment. A client who reads
    // "Working" and assumes invitations went out is worse off than one who was
    // told nothing.
    expect(describeStatus("PendingClientApproval").blurb).toMatch(/nobody has been contacted/i);
  });

  it("marks a failure as having sent nothing", () => {
    expect(describeStatus("Failed").tone).toBe("danger");
    expect(describeStatus("Failed").blurb).toMatch(/nothing was sent/i);
  });

  it("returns a usable object for a status it has never seen", () => {
    // A new server state must not render an empty card. Showing the raw value
    // is honest; showing "Unknown" would imply the server had nothing to say.
    const result = describeStatus("SomeStatusAddedIn2027");
    expect(result.label).toBe("SomeStatusAddedIn2027");
    expect(result.tone).toBe("neutral");
  });

  it("handles a missing status without throwing", () => {
    expect(describeStatus(undefined).label).toBe("Unknown");
    expect(describeStatus(null).label).toBe("Unknown");
  });

  it("uses only tones the design system has", () => {
    for (const status of SERVER_STATUSES) {
      expect(["neutral", "info", "warn", "danger", "ok"]).toContain(describeStatus(status).tone);
    }
  });
});

describe("the client approval gate", () => {
  it("opens only for PendingClientApproval", () => {
    expect(canApproveShortlist("PendingClientApproval")).toBe(true);
    for (const status of SERVER_STATUSES.filter((s) => s !== "PendingClientApproval")) {
      expect(canApproveShortlist(status), `${status} must not offer the client gate`).toBe(false);
    }
  });

  it("is not offered at the manager gate", () => {
    // The single most important assertion in this file. If the client gate
    // opened at PendingManagerApproval, a client could send invitations at the
    // point where the platform is waiting for a manager to authorise a contract.
    expect(canApproveShortlist("PendingManagerApproval")).toBe(false);
  });

  it("closes once the run has been approved", () => {
    // Re-approving is refused server-side, so a control that is still live after
    // approval is a button that is guaranteed to fail.
    expect(canApproveShortlist("Approved")).toBe(false);
    expect(canApproveShortlist("Completed")).toBe(false);
  });
});

describe("the manager approval gate", () => {
  it("opens for PendingManagerApproval", () => {
    expect(canApproveContract("PendingManagerApproval")).toBe(true);
  });

  it("also opens for the legacy PendingApproval", () => {
    // `PendingApproval = 2` predates the split and is still what a run that
    // predates the migration holds. It means "waiting for a decision" and the
    // manager is who decides, so it is treated the same.
    expect(canApproveContract("PendingApproval")).toBe(true);
  });

  it("is not open at the client gate", () => {
    expect(canApproveContract("PendingClientApproval")).toBe(false);
  });
});

describe("waiting-on-a-human helpers", () => {
  it("separates the two gates", () => {
    expect(isWaitingOnClient("PendingClientApproval")).toBe(true);
    expect(isWaitingOnManager("PendingClientApproval")).toBe(false);

    expect(isWaitingOnManager("PendingManagerApproval")).toBe(true);
    expect(isWaitingOnClient("PendingManagerApproval")).toBe(false);
  });

  it("reports neither while a run is simply working", () => {
    expect(isWaitingOnClient("Running")).toBe(false);
    expect(isWaitingOnManager("Running")).toBe(false);
  });
});

describe("pollIntervalFor", () => {
  it("polls a run that is in progress", () => {
    expect(pollIntervalFor("Running")).toBeGreaterThan(0);
    expect(pollIntervalFor("Pending")).toBeGreaterThan(0);
  });

  it("stops on every terminal state", () => {
    for (const status of TERMINAL_STATUSES) {
      expect(pollIntervalFor(status), `${status} will never change again`).toBe(false);
    }
  });

  it("stops while a human is reading", () => {
    // A run waiting for approval will not move until someone decides. Polling
    // it every three seconds for as long as they take to read a shortlist is
    // pure waste, and it repaints the page under the approve button -- which
    // makes the button feel unsafe to press.
    expect(pollIntervalFor("PendingClientApproval")).toBe(false);
    expect(pollIntervalFor("PendingManagerApproval")).toBe(false);
    // The pre-split status. It predates the migration, so a run can still hold
    // it, and it is a human gate for the same reason.
    expect(pollIntervalFor("PendingApproval")).toBe(false);
  });

  it("stops for a status it does not know", () => {
    // An allow-list, not a deny-list. Polling a status this build has never
    // heard of means a spinner that never stops and a request every three
    // seconds until the tab closes -- and neither looks like a bug, they look
    // like the server being slow. A stale page is the better failure.
    expect(pollIntervalFor("SomeStatusAddedIn2027")).toBe(false);
    expect(pollIntervalFor("approved")).toBe(false); // wrong case, not a match
  });

  it("does not poll before there is a status", () => {
    expect(pollIntervalFor(undefined)).toBe(false);
    expect(pollIntervalFor(null)).toBe(false);
    expect(pollIntervalFor("")).toBe(false);
  });

  it("polls exactly the statuses that move on their own", () => {
    // The whole contract in one assertion: two statuses poll, everything else
    // does not. Written from the allow-list so it cannot contradict the two
    // tests above.
    const polling = SERVER_STATUSES.filter((s) => pollIntervalFor(s) !== false);
    expect(polling).toEqual(["Pending", "Running"]);
  });
});

describe("hasShortlist", () => {
  it("is false for an empty or absent list", () => {
    expect(hasShortlist([])).toBe(false);
    expect(hasShortlist(undefined)).toBe(false);
    expect(hasShortlist(null)).toBe(false);
  });

  it("is false for a non-array, so a malformed response cannot enable the gate", () => {
    // The approve control must never appear because the server returned an
    // object where a list was expected.
    expect(hasShortlist({ shortlist: [] })).toBe(false);
    expect(hasShortlist("none")).toBe(false);
  });

  it("is true for a non-empty list", () => {
    expect(hasShortlist([{ freelancerId: "a" }])).toBe(true);
  });
});

describe("formatScore", () => {
  it("renders the server's 0-100 scale unchanged", () => {
    // The server sends 0-100. Dividing by 100 again shows "0.9%" for the
    // strongest possible match, which reads as a bug to the user.
    expect(formatScore(87.5)).toBe("88%");
    expect(formatScore(100)).toBe("100%");
    expect(formatScore(0)).toBe("0%");
  });

  it("scales a value that arrived as a proportion", () => {
    // Defensive: if the scale ever changes at the source, a 0.875 should read as
    // 88% rather than as "1%".
    expect(formatScore(0.875)).toBe("88%");
    expect(formatScore(1)).toBe("100%");
  });

  it("shows a dash for a missing score rather than NaN", () => {
    // "NaN%" on a card is worse than no number: it looks like a rendering
    // failure rather than an absent value.
    expect(formatScore(null)).toBe("—");
    expect(formatScore(undefined)).toBe("—");
    expect(formatScore("not a number")).toBe("—");
  });
});

describe("shortlistKey", () => {
  it("keys on the freelancer id, not the array index", () => {
    // The list is sorted by score, so an index key remounts a row when the order
    // changes and swallows a click mid-interaction.
    expect(shortlistKey({ freelancerId: "abc", score: 90 })).toBe("abc");
    expect(shortlistKey({ id: 7 })).toBe(7);
  });

  it("produces a stable key for a bare row", () => {
    expect(shortlistKey("abc")).toBe("abc");
  });
});
