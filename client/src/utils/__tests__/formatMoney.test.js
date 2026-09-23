import { describe, expect, it } from "vitest";
import formatMoney from "../formatMoney.js";

describe("formatMoney", () => {
  it("formats positive values as USD", () => {
    expect(formatMoney(1234)).toBe("$1,234.00");
  });

  it("formats zero values as USD", () => {
    expect(formatMoney(0)).toBe("$0.00");
  });

  it("formats large values as USD", () => {
    expect(formatMoney(123456789.5)).toBe("$123,456,789.50");
  });
});
