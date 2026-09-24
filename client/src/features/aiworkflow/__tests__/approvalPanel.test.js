import { describe, expect, it } from "vitest";
import { workflowState } from "../workflowDisplay.js";

describe("AI workflow approval panel decisions", () => {
  it("offers approval actions for pending approval", () => {
    const state = workflowState({ status: "PendingApproval" });
    expect(state.canApprove).toBe(true);
    expect(state.status).toBe("PendingApproval");
  });
  it("hides approval actions for completed workflows", () => {
    expect(workflowState({ status: "Completed" }).canApprove).toBe(false);
  });
});
