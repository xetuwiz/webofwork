import { describe, expect, it } from "vitest";
import { getAdminErrorMessage } from "../adminErrorState.js";

describe("admin contract operation errors", () => {
  it("returns a safe message for a network error", () => {
    expect(getAdminErrorMessage({ code: "ERR_NETWORK" })).toBe("Unable to reach the server. Check your connection.");
  });

  it("prefers a server detail message", () => {
    expect(getAdminErrorMessage({ response: { data: { detail: "Dispute was already resolved." } } })).toBe("Dispute was already resolved.");
  });

  it("falls back to a generic message", () => {
    expect(getAdminErrorMessage({})).toBe("Something went wrong. Please try again.");
  });
});
