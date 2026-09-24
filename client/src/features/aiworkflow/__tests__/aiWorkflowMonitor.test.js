import { describe, expect, it } from "vitest";
import { workflowRows, workflowState } from "../workflowDisplay.js";

describe("AI workflow monitor display", () => {
  it("normalizes paginated and legacy workflow responses", () => {
    expect(workflowRows({ results: [{ id: 1 }] })).toHaveLength(1);
    expect(workflowRows([{ id: 2 }])).toHaveLength(1);
    expect(workflowRows(null)).toEqual([]);
  });
  it("marks approval states correctly", () => {
    expect(workflowState({ status: "PendingApproval" }).canApprove).toBe(true);
    expect(workflowState({ status: "Completed" }).canApprove).toBe(false);
  });
});
