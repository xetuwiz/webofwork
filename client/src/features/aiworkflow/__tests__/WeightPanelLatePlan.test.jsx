import { describe, expect, it, vi } from "vitest";
import { render, screen } from "@testing-library/react";

import WeightPanel from "../WeightPanel";
import { DEFAULT_SHORTLIST_SIZE } from "../workflowDisplay";

/**
 * Does the panel show the weights the run actually produced?
 *
 * ## The failure this exists for
 *
 * The client polls the shortlist while the AI works. On the first poll there is
 * no plan yet -- the run has not finished -- so `readPlan` yields the defaults
 * and `useState` captures them.
 *
 * `initial` is recomputed on every render but `weights` is captured once. When
 * the run completed and the real plan arrived, the caption updated (it reads
 * `initial` fresh) while the sliders did not. Observed on run 25:
 *
 *   planner chose  skill_coverage 0.35, portfolio_relevance 0.25,
 *                   past_success_rate 0.15, availability_and_speed 0.15,
 *                   budget_fit 0.10
 *   panel showed   35%  20%  20%  15%  10%     <- the defaults
 *   caption read   "These are the weights the AI chose for this shortlist."
 *
 * A caption asserting provenance above sliders that do not match it is worse than
 * no caption: it claims a specificity the numbers underneath do not have.
 */

/** The plan run 25's planner produced, as the endpoint returns it. */
const PLANNER_PLAN = {
  weights: {
    skill_coverage: 0.35,
    portfolio_relevance: 0.25,
    past_success_rate: 0.15,
    availability_and_speed: 0.15,
    budget_fit: 0.1,
  },
  shortlist_size: 10,
};

/** The endpoint's shape: `plan` arrives as a JSON *string*. */
const asString = (plan) => JSON.stringify(plan);

function renderPanel(props = {}) {
  return render(
    <WeightPanel plan={null} planSource={null} onRerun={() => {}} {...props} />,
  );
}

/**
 * Reads a slider's current value by its visible label.
 *
 * By label rather than by position: these tests are about *which* number is
 * on which slider, and a positional lookup would keep passing if the order
 * changed underneath them.
 */
function slider(labelText) {
  return screen.getByRole("slider", { name: new RegExp(labelText, "i") });
}

describe("WeightPanel adopting a late plan", () => {
  it("starts on the defaults when there is no plan", () => {
    renderPanel();
    expect(slider("Relevant past work").value).toBe("20");
    expect(
      screen.getByText(/has not chosen its weights yet/i),
    ).toBeInTheDocument();
  });

  it("adopts the plan when it arrives after first render", () => {
    // The exact sequence: no plan on the first poll, then the finished run.
    const { rerender } = renderPanel();

    expect(slider("Relevant past work").value).toBe("20");

    rerender(
      <WeightPanel
        plan={asString(PLANNER_PLAN)}
        planSource="planner"
        onRerun={() => {}}
      />,
    );

    // 25, not the default 20. This is the whole test.
    expect(slider("Relevant past work").value).toBe("25");
    expect(slider("Past success rate").value).toBe("15");
    expect(slider("Skill coverage").value).toBe("35");
  });

  it("shows the caption that matches the numbers it is now showing", () => {
    // The disagreement that made the bug visible: a fresh caption over stale
    // numbers. Asserted together so the two can never drift apart again.
    const { rerender } = renderPanel();
    rerender(
      <WeightPanel
        plan={asString(PLANNER_PLAN)}
        planSource="planner"
        onRerun={() => {}}
      />,
    );

    expect(screen.getByText(/the AI chose/i)).toBeInTheDocument();
    expect(slider("Relevant past work").value).toBe("25");
    expect(screen.queryByText(/has not chosen its weights yet/i)).toBeNull();
  });

  it("adopts a plan supplied by a person on a re-run", () => {
    const { rerender } = renderPanel();
    rerender(
      <WeightPanel
        plan={asString({
          weights: {
            skill_coverage: 0.9,
            portfolio_relevance: 0.04,
            past_success_rate: 0.03,
            availability_and_speed: 0.02,
            budget_fit: 0.01,
          },
          shortlist_size: 3,
        })}
        planSource="supplied"
        onRerun={() => {}}
      />,
    );

    expect(slider("Skill coverage").value).toBe("90");
    expect(screen.getByText(/you set/i)).toBeInTheDocument();
  });

  it("adopts a different shortlist size too", () => {
    const { rerender } = renderPanel();
    rerender(
      <WeightPanel
        plan={asString({ ...PLANNER_PLAN, shortlist_size: 7 })}
        planSource="planner"
        onRerun={() => {}}
      />,
    );

    const size = screen.getByRole("slider", { name: /how many to shortlist/i });
    expect(size.value).toBe("7");
  });

  it("leaves a dragged slider alone when the plan is re-rendered unchanged", () => {
    // The reset is keyed on the plan's content, so an unrelated re-render must
    // not discard what the user is holding.
    const { rerender } = renderPanel({
      plan: asString(PLANNER_PLAN),
      planSource: "planner",
    });

    const target = slider("Skill coverage", "x");
    target.focus();

    rerender(
      <WeightPanel
        plan={asString(PLANNER_PLAN)}
        planSource="planner"
        onRerun={() => {}}
        isRerunning={false}
      />,
    );

    expect(target.value).toBe("35");
  });

  it("still refuses a plan that does not total 1", () => {
    // The guard that predates all of this must survive the adoption path: a plan
    // the server would reject has to be visible as a problem, not silently
    // adopted.
    renderPanel({
      plan: asString({
        weights: {
          skill_coverage: 0.9,
          portfolio_relevance: 0.9,
          past_success_rate: 0.1,
          availability_and_speed: 0.1,
          budget_fit: 0.1,
        },
        shortlist_size: 10,
      }),
      planSource: "planner",
    });

    expect(screen.getByRole("status")).toHaveTextContent("Total: 2.10 of 1.00");
    expect(screen.getByText(/add up to 2\.10, not 1/i)).toBeInTheDocument();
    expect(
      screen.getByRole("button", { name: /re-run with these weights/i }),
    ).toBeDisabled();
  });

  it("keeps the default shortlist size when the plan omits one", () => {
    renderPanel({
      plan: asString({ weights: PLANNER_PLAN.weights }),
      planSource: "planner",
    });

    expect(screen.getByRole("slider", { name: /how many to shortlist/i }).value).toBe(
      String(DEFAULT_SHORTLIST_SIZE),
    );
  });

  it("ignores a plan that will not parse rather than blanking the sliders", () => {
    renderPanel({ plan: "{not json", planSource: "planner" });

    expect(slider("Relevant past work").value).toBe("20");
    expect(screen.getByText(/has not chosen its weights yet/i)).toBeInTheDocument();
  });

  it("does not call onRerun just because a plan arrived", () => {
    // Adopting the server's numbers is not a user action. If this fired, a
    // re-run would be dispatched on the client's behalf.
    const onRerun = vi.fn();
    const { rerender } = renderPanel({ onRerun });
    rerender(
      <WeightPanel
        plan={asString(PLANNER_PLAN)}
        planSource="planner"
        onRerun={onRerun}
      />,
    );

    expect(onRerun).not.toHaveBeenCalled();
  });
});