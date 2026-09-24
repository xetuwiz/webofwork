import { describe, expect, it } from "vitest";
import { toDisputePayload, validateDisputeForm } from "../disputeForm.js";

describe("dispute creation form", () => {
  it("rejects missing or short values", () => {
    const result = validateDisputeForm({ reason: "", description: "short" });
    expect(result.valid).toBe(false);
    expect(result.errors.reason).toBeTruthy();
    expect(result.errors.description).toBeTruthy();
  });

  it("accepts a valid reason and detailed description", () => {
    expect(validateDisputeForm({ reason: "Scope disagreement", description: "The requested work exceeds the agreed scope." }).valid).toBe(true);
  });

  it("builds the backend-compatible payload", () => {
    expect(toDisputePayload(42, { reason: " Scope disagreement ", description: " The requested work exceeds scope. " })).toEqual({
      contractId: 42,
      reason: "Scope disagreement",
      description: "The requested work exceeds scope.",
      evidenceUrls: [],
    });
  });

  it("does not build a payload for invalid input", () => {
    expect(() => toDisputePayload(42, { reason: "", description: "" })).toThrow("Invalid dispute form");
  });
});
