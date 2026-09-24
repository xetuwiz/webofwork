import { describe, expect, it } from "vitest";
import { getContractState } from "../contractDisplay.js";

describe("contract state display", () => {
  it.each([
    ["Offered", "Accept contract", null],
    ["Active", "Request revision", "Submit deliverable"],
    ["Submitted", "Approve work", null],
    ["Completed", null, null],
  ])("maps %s to the correct client/freelancer actions", (status, clientAction, freelancerAction) => {
    const state = getContractState(status);
    expect(state.label).toBe(status);
    expect(state.clientAction).toBe(clientAction);
    expect(state.freelancerAction).toBe(freelancerAction);
  });

  it("falls back safely for unknown statuses", () => {
    expect(getContractState("future-state")).toEqual({ label: "future-state", clientAction: null, freelancerAction: null });
  });
});
