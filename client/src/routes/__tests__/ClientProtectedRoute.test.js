import { describe, expect, it } from "vitest";
import { canAccessClientRoute } from "../clientRouteAccess.js";

describe("canAccessClientRoute", () => {
  it("allows authenticated client users", () => {
    expect(canAccessClientRoute({ role: "client" })).toBe(true);
    expect(canAccessClientRoute({ role: 1 })).toBe(true);
  });

  it("blocks unauthenticated users", () => {
    expect(canAccessClientRoute(null)).toBe(false);
    expect(canAccessClientRoute(undefined)).toBe(false);
  });

  it("blocks non-client roles", () => {
    expect(canAccessClientRoute({ role: "freelancer" })).toBe(false);
    expect(canAccessClientRoute({ role: "admin" })).toBe(false);
  });
});
