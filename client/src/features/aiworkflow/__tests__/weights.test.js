/**
 * The scoring weights.
 *
 * ## Why the maths is tested rather than eyeballed
 *
 * The weights are relative and must total 1. Everything about the control
 * follows from that one constraint:
 *
 *  - a set totalling 1.4 over-scores every candidate by 40%, uniformly. That
 *    does not look like a bug -- it looks like a strong shortlist. So it would
 *    never be reported, which is exactly why it has to be impossible rather than
 *    discouraged.
 *  - the factor being dragged has to keep its value while the others absorb the
 *    difference, or the slider jumps out from under the pointer.
 *  - and a plan that fails any of this must be refused *before* the request,
 *    with a reason the user can act on.
 */

import { describe, expect, it } from "vitest";

import {
  DEFAULT_SHORTLIST_SIZE,
  DEFAULT_WEIGHTS,
  WEIGHT_FACTORS,
  normaliseAround,
  planProblem,
  readPlan,
  totalWeight,
} from "../workflowDisplay";

const KEYS = WEIGHT_FACTORS.map((f) => f.key);

describe("the weight vocabulary", () => {
  it("has five factors, which is what the matcher scores", () => {
    // Not an arbitrary number. A factor the matcher ignores would be a slider
    // that does nothing, and one it scores that is missing here would be a
    // decision the client cannot make.
    expect(KEYS).toEqual([
      "skill_coverage",
      "portfolio_relevance",
      "past_success_rate",
      "availability_and_speed",
      "budget_fit",
    ]);
  });

  it("every factor has a label and a hint, so none renders as a bare key", () => {
    for (const factor of WEIGHT_FACTORS) {
      expect(factor.label, factor.key).toBeTruthy();
      expect(factor.hint, `${factor.key} has no explanation`).toBeTruthy();
    }
  });

  it("the defaults are valid, so the panel never opens in a refused state", () => {
    expect(totalWeight(DEFAULT_WEIGHTS)).toBeCloseTo(1, 6);
    expect(planProblem(DEFAULT_WEIGHTS, DEFAULT_SHORTLIST_SIZE)).toBeNull();
  });
});

describe("readPlan", () => {
  it("reads the JSON string the server actually sends", () => {
    // `WorkflowRun.Plan` is jsonb held as text, so the endpoint returns a
    // string. Parsing only an object would silently fall back to the defaults
    // and the client would be shown sliders that do not match the scores above
    // them -- which looks like the scores being wrong.
    const plan = readPlan(
      JSON.stringify({
        weights: { skill_coverage: 0.6, budget_fit: 0.4 },
        shortlist_size: 7,
      }),
    );

    expect(plan.weights.skill_coverage).toBe(0.6);
    expect(plan.shortlistSize).toBe(7);
    expect(plan.fromServer).toBe(true);
  });

  it("reads an object too, in case the endpoint ever stops stringifying", () => {
    const plan = readPlan({ weights: { skill_coverage: 0.6 }, shortlist_size: 7 });
    expect(plan.weights.skill_coverage).toBe(0.6);
  });

  it("fills the factors the server did not send", () => {
    // A partial plan must not leave a slider at zero, which would read as "this
    // factor does not matter" rather than "we were not told".
    const plan = readPlan(JSON.stringify({ weights: { skill_coverage: 0.6 } }));
    expect(plan.weights.budget_fit).toBe(DEFAULT_WEIGHTS.budget_fit);
  });

  it("falls back to the defaults for a plan it cannot read", () => {
    for (const bad of [undefined, null, "", "not json", "[1,2]", 42, "{"]) {
      const plan = readPlan(bad);
      expect(plan.fromServer, `readPlan(${JSON.stringify(bad)})`).toBe(false);
      expect(plan.weights).toEqual(DEFAULT_WEIGHTS);
    }
  });

  it("ignores a non-numeric weight rather than storing NaN", () => {
    // NaN in a range input renders as an empty track, and `totalWeight` would
    // then be NaN, which fails every comparison and slips through validation.
    const plan = readPlan(JSON.stringify({ weights: { skill_coverage: "lots" } }));
    expect(plan.weights.skill_coverage).toBe(DEFAULT_WEIGHTS.skill_coverage);
  });
});

describe("planProblem", () => {
  it("accepts a valid set", () => {
    expect(planProblem({ ...DEFAULT_WEIGHTS }, 10)).toBeNull();
  });

  it("refuses a set that does not total one, and says what it totals", () => {
    // 0.9 + 0.2 + 0.2 + 0.15 + 0.1 = 1.55
    const problem = planProblem({ ...DEFAULT_WEIGHTS, skill_coverage: 0.9 }, 10);
    expect(problem).toContain("1.55");
    // The explanation is the point: "not 1" alone leaves the user guessing.
    expect(problem).toMatch(/relative/i);
  });

  it("refuses a weight outside zero to one", () => {
    // A set of {-1, 0, 0, 0, 2} totals 1, so the sum check alone passes it.
    const problem = planProblem(
      { skill_coverage: -1, portfolio_relevance: 0, past_success_rate: 0, availability_and_speed: 0, budget_fit: 2 },
      10,
    );
    expect(problem).toMatch(/between 0 and 1/i);
  });

  it("refuses a NaN weight, which would pass every naive comparison", () => {
    expect(planProblem({ ...DEFAULT_WEIGHTS, skill_coverage: NaN }, 10)).not.toBeNull();
  });

  it("refuses a shortlist size outside five to fifteen", () => {
    for (const size of [0, 4, 16, 100, 10.5]) {
      expect(planProblem({ ...DEFAULT_WEIGHTS }, size), `size ${size}`).not.toBeNull();
    }
  });

  it("accepts the boundary sizes", () => {
    for (const size of [5, 15]) {
      expect(planProblem({ ...DEFAULT_WEIGHTS }, size), `size ${size}`).toBeNull();
    }
  });
});

describe("normaliseAround", () => {
  it("keeps the dragged factor exactly where it was put", () => {
    // The whole reason for this function. If the dragged value moved, the slider
    // would jump under the pointer and every subsequent drag would be relative
    // to a number the user did not choose.
    const next = normaliseAround({ ...DEFAULT_WEIGHTS }, "skill_coverage", 0.6);
    expect(next.skill_coverage).toBe(0.6);
  });

  it("makes the whole set total one", () => {
    for (const value of [0, 0.1, 0.35, 0.5, 0.9, 1]) {
      for (const key of KEYS) {
        const next = normaliseAround({ ...DEFAULT_WEIGHTS }, key, value);
        expect(totalWeight(next), `${key} at ${value}`).toBeCloseTo(1, 3);
      }
    }
  });

  it("keeps every weight within bounds", () => {
    // Otherwise the panel can produce a set its own validator refuses, which is
    // a control that blocks itself.
    for (const value of [0, 0.25, 0.75, 1]) {
      for (const key of KEYS) {
        const next = normaliseAround({ ...DEFAULT_WEIGHTS }, key, value);
        for (const k of KEYS) {
          expect(next[k], `${key}=${value} pushed ${k} out of range`).toBeGreaterThanOrEqual(0);
          expect(next[k], `${key}=${value} pushed ${k} out of range`).toBeLessThanOrEqual(1);
        }
      }
    }
  });

  it("spreads the remainder evenly when the other weights are all zero", () => {
    // No proportional split is possible with nothing to scale. Assigning the
    // whole remainder to one factor would be a far larger change than the drag
    // asked for.
    const zeros = Object.fromEntries(KEYS.map((k) => [k, 0]));
    const next = normaliseAround(zeros, "skill_coverage", 0.5);

    const others = KEYS.filter((k) => k !== "skill_coverage");
    for (const key of others) {
      expect(next[key], `${key} should take an equal share`).toBeCloseTo(0.125, 3);
    }
  });

  it("preserves the relative order of the other weights", () => {
    // A drag on one factor should not invert the relative importance of the
    // others, which is not what the user asked for and is invisible until
    // someone notices the ranking changed for a strange reason.
    const before = {
      skill_coverage: 0.1,
      portfolio_relevance: 0.4,
      past_success_rate: 0.3,
      availability_and_speed: 0.15,
      budget_fit: 0.05,
    };
    const after = normaliseAround(before, "skill_coverage", 0.2);

    const others = KEYS.filter((k) => k !== "skill_coverage");
    const beforeOrder = [...others].sort((a, b) => before[b] - before[a]);
    const afterOrder = [...others].sort((a, b) => after[b] - after[a]);

    expect(afterOrder).toEqual(beforeOrder);
  });

  it("produces a set its own validator accepts, for every drag", () => {
    // The composition that matters: if rescaling can produce a refused plan, the
    // panel can disable its own button with no way for the user to fix it.
    for (const key of KEYS) {
      for (let percent = 0; percent <= 100; percent += 5) {
        const next = normaliseAround({ ...DEFAULT_WEIGHTS }, key, percent / 100);
        expect(planProblem(next, 10), `${key} at ${percent}% produced a refused plan`).toBeNull();
      }
    }
  });
});
