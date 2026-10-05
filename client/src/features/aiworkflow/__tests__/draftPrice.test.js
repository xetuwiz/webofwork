import { describe, it, expect } from "vitest";
import { formatDraftPrice } from "../draftPrice";

/**
 * The review panel's money row.
 *
 * ## The live bug
 *
 * Run 21 proposed a $700 - $2,000 range. The panel said `Price 700.0`, and the
 * form was then filled with 700 and 2000.
 *
 *     ["Price", parsed.fixed_price_usd ?? parsed.min_budget_usd ?? parsed.max_budget_usd]
 *
 * `??` stops at the first defined value, so a range showed only its lower bound.
 * The maximum -- the number most likely to be wrong, and the one a client would
 * most want to check -- was never on screen. The panel exists to be the last
 * thing read before "Fill the form", so an omitted number is a number agreed to
 * without looking at it.
 */

describe("formatDraftPrice", () => {
  describe("a range", () => {
    it("shows both bounds", () => {
      // The whole bug. Before: "$700", from `min_budget_usd` alone.
      expect(
        formatDraftPrice({
          pricing_type: "range",
          min_budget_usd: 700,
          max_budget_usd: 2000,
        })
      ).toBe("$700 - $2,000");
    });

    it("never shows a lone number that reads as a complete range", () => {
      const minOnly = formatDraftPrice({
        pricing_type: "range",
        min_budget_usd: 700,
      });

      expect(minOnly).toContain("no maximum");
      expect(minOnly).not.toBe("$700");
    });

    it("says which bound is missing when only the maximum arrived", () => {
      expect(
        formatDraftPrice({ pricing_type: "range", max_budget_usd: 2000 })
      ).toBe("up to $2,000 (no minimum)");
    });

    it("shows nothing when a range has neither bound", () => {
      expect(formatDraftPrice({ pricing_type: "range" })).toBeNull();
    });

    it("treats a zero minimum as a real bound, not as absent", () => {
      // Not for the reason a first reading suggests. `money()` returns a *string*,
      // and "0" is truthy in JavaScript, so `if (min)` already handles this
      // correctly -- an earlier version of this comment claimed otherwise and was
      // wrong, which a mutation confirmed by surviving the naive fix.
      //
      // Worth keeping as a guard anyway: the day `money` returns a number instead
      // of a string, this is the test that notices.
      expect(
        formatDraftPrice({
          pricing_type: "range",
          min_budget_usd: 0,
          max_budget_usd: 500,
        })
      ).toBe("$0 - $500");
    });

    it("groups thousands", () => {
      expect(
        formatDraftPrice({
          pricing_type: "range",
          min_budget_usd: 1500,
          max_budget_usd: 12500,
        })
      ).toBe("$1,500 - $12,500");
    });
  });

  describe("a fixed price", () => {
    it("shows the single figure", () => {
      expect(
        formatDraftPrice({ pricing_type: "fixed", fixed_price_usd: 2500 })
      ).toBe("$2,500");
    });

    it("keeps cents when they are real", () => {
      expect(
        formatDraftPrice({ pricing_type: "fixed", fixed_price_usd: 2499.99 })
      ).toBe("$2,499.99");
    });

    it("does not invent cents that were never there", () => {
      // Live output was "700.0", which reads as a computed estimate rather than
      // the number a person would type.
      expect(
        formatDraftPrice({ pricing_type: "fixed", fixed_price_usd: 700 })
      ).toBe("$700");
    });

    it("ignores range fields when the type is fixed", () => {
      // `applyDraft` reads the fields per `pricing_type` and so must this, or the
      // panel and the form disagree about what is being proposed.
      //
      // The fixed figure is present here on purpose, but the assertion that
      // actually discriminates is the next one -- with `fixed_price_usd` supplied,
      // a fallback to `min_budget_usd` produces the same answer and the test
      // passes either way.
      expect(
        formatDraftPrice({
          pricing_type: "fixed",
          fixed_price_usd: 700,
          min_budget_usd: 100,
          max_budget_usd: 200,
        })
      ).toBe("$700");
    });

    it("shows nothing rather than borrowing a bound from the other pricing mode", () => {
      // The discriminating case. A model that filled `min_budget_usd` but typed
      // `pricing_type: "fixed"` has said something contradictory; `applyDraft`
      // would fill neither box, so the panel must not quietly display $100 as if
      // a fixed price had been proposed.
      expect(
        formatDraftPrice({
          pricing_type: "fixed",
          min_budget_usd: 100,
          max_budget_usd: 200,
        })
      ).toBeNull();
    });
  });

  describe("nothing usable", () => {
    it.each([
      [{}, "no pricing at all"],
      [{ pricing_type: "fixed" }, "fixed with no figure"],
      [{ pricing_type: "fixed", fixed_price_usd: null }, "an explicit null"],
      [{ pricing_type: "fixed", fixed_price_usd: "2500" }, "a numeric string"],
      [{ pricing_type: "fixed", fixed_price_usd: NaN }, "NaN"],
      [{ pricing_type: "fixed", fixed_price_usd: Infinity }, "Infinity"],
    ])("shows nothing for %j (%s)", (input) => {
      expect(formatDraftPrice(input)).toBeNull();
    });

    it("survives being handed nothing", () => {
      expect(formatDraftPrice(undefined)).toBeNull();
      expect(formatDraftPrice(null)).toBeNull();
    });
  });
});
