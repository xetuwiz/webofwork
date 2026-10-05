import React, { useState } from "react";
import { SlidersHorizontal, RotateCcw, AlertTriangle, Loader2 } from "lucide-react";
import Button from "../../components/common/Button";
import { weightCaption } from "./weightCaption";
import {
  DEFAULT_SHORTLIST_SIZE,
  DEFAULT_WEIGHTS,
  WEIGHT_FACTORS,
  normaliseAround,
  planProblem,
  readPlan,
  totalWeight,
} from "./workflowDisplay";

/**
 * The scoring weights behind a shortlist, and the control to change them.
 *
 * ## Why the weights are shown at all
 *
 * A match score of 87 means nothing to a client on its own. The weights are what
 * make it legible: this is how much the score values skills against budget
 * against past reliability. Hiding them would mean asking someone to act on a
 * number produced by a system they cannot inspect.
 *
 * ## Why dragging one slider rescales the others
 *
 * The weights are relative, and the server requires them to total 1. A control
 * that let them total 1.4 would produce scores inflated by 40% across the board
 * -- which looks like a stronger shortlist rather than a mistake, so it would
 * never be reported.
 *
 * So the factor being dragged keeps its value and the rest absorb the
 * difference, proportionally. That is the only version where the slider under
 * the cursor stays where the user put it.
 *
 * ## It says where the numbers came from
 *
 * `fromServer` distinguishes weights the planner chose from weights a person
 * set. Presenting the first as though it were the second is the specific
 * dishonesty this panel exists to avoid -- the client is shown a plan and given
 * the impression they reviewed it.
 *
 * `planSource` is the server's own answer to that question ("planner" or
 * "supplied"), so the caption can be exact rather than inferred. It was missing
 * before, and `fromServer` was the only signal -- which meant a first run, whose
 * plan lives in the run's output rather than its plan column, was captioned as
 * having no weights at all.
 *
 * The wording itself is in `./weightCaption`, not here, so it can be tested
 * without rendering the panel and so this module keeps exporting one component.
 */
export default function WeightPanel({
  plan,
  planSource,
  onRerun,
  isRerunning = false,
  disabled = false,
}) {
  const initial = readPlan(plan);

  const [weights, setWeights] = useState(initial.weights);
  const [shortlistSize, setShortlistSize] = useState(initial.shortlistSize);

  // Adopt a plan that arrives after the panel has already rendered.
  //
  // `initial` is recomputed every render, but `weights` is captured once by
  // `useState`. On a page that is open while the run finishes -- the ordinary
  // flow, since the shortlist polls until the AI is done -- the first render sees
  // no plan and locks in the defaults, and every later render recomputes
  // `initial` without touching the sliders.
  //
  // The visible result was a caption reading "These are the weights the AI chose
  // for this shortlist" directly above five sliders sitting at the defaults. The
  // caption reads `initial` fresh, so it updated; the sliders did not. Observed on
  // run 25, where the planner chose 35/25/15/15/10 and the panel showed
  // 35/20/20/15/10.
  //
  // Keyed on the plan's *content*, not on `initial`, so a re-render with an
  // equivalent plan is a no-op and a slider the user is dragging is never yanked
  // out from under them mid-drag. The ref is state rather than an effect's
  // dependency precisely because the reset has to happen during render, before
  // the stale values are painted.
  const planKey = JSON.stringify([initial.fromServer, initial.weights, initial.shortlistSize]);
  const [adoptedPlan, setAdoptedPlan] = useState(planKey);

  if (planKey !== adoptedPlan) {
    setAdoptedPlan(planKey);
    setWeights(initial.weights);
    setShortlistSize(initial.shortlistSize);
  }

  const problem = planProblem(weights, shortlistSize);
  const total = totalWeight(weights);
  const changed =
    WEIGHT_FACTORS.some(({ key }) => weights[key] !== initial.weights[key]) ||
    shortlistSize !== initial.shortlistSize;

  const reset = () => {
    setWeights({ ...DEFAULT_WEIGHTS });
    setShortlistSize(DEFAULT_SHORTLIST_SIZE);
  };

  const drag = (key, value) =>
    setWeights((current) => normaliseAround(current, key, value));

  const submit = () => {
    if (problem || !changed) return;
    onRerun?.({ weights: { ...weights }, shortlistSize });
  };

  return (
    <section className="bg-surface rounded-2xl border border-line overflow-hidden">
      <header className="p-6 sm:p-8 flex flex-col sm:flex-row sm:items-start sm:justify-between gap-3">
        <div>
          <h2 className="font-bold text-fg flex items-center gap-2">
            <SlidersHorizontal size={18} className="text-brand" aria-hidden="true" />
            How candidates are scored
          </h2>
          <p className="text-fg-muted text-sm mt-1 max-w-prose">
            {weightCaption(initial, planSource)}
          </p>
        </div>

        {changed && (
          <Button variant="ghost" size="sm" onClick={reset} disabled={isRerunning}>
            <RotateCcw size={14} className="mr-1.5" aria-hidden="true" />
            Reset
          </Button>
        )}
      </header>

      <div className="px-6 sm:px-8 pb-2 space-y-5">
        {WEIGHT_FACTORS.map(({ key, label, hint }) => {
          const percent = Math.round(weights[key] * 100);
          return (
            <div key={key}>
              <div className="flex items-baseline justify-between gap-3">
                <label
                  htmlFor={`weight-${key}`}
                  className="text-sm font-semibold text-fg"
                >
                  {label}
                </label>
                <span className="text-sm font-bold text-brand tabular-nums">{percent}%</span>
              </div>

              <input
                id={`weight-${key}`}
                type="range"
                min="0"
                max="100"
                step="1"
                value={percent}
                disabled={disabled || isRerunning}
                onChange={(event) => drag(key, Number(event.target.value) / 100)}
                aria-describedby={`weight-${key}-hint`}
                className="mt-1.5 w-full accent-[var(--brand)] disabled:opacity-50"
              />

              <p id={`weight-${key}-hint`} className="text-xs text-fg-muted mt-0.5">
                {hint}
              </p>
            </div>
          );
        })}

        <div className="pt-2 border-t border-line-soft">
          <div className="flex items-baseline justify-between gap-3">
            <label htmlFor="weight-size" className="text-sm font-semibold text-fg">
              How many to shortlist
            </label>
            <span className="text-sm font-bold text-brand tabular-nums">{shortlistSize}</span>
          </div>
          <input
            id="weight-size"
            type="range"
            min="5"
            max="15"
            step="1"
            value={shortlistSize}
            disabled={disabled || isRerunning}
            onChange={(event) => setShortlistSize(Number(event.target.value))}
            className="mt-1.5 w-full accent-[var(--brand)] disabled:opacity-50"
          />
        </div>
      </div>

      <footer className="p-6 sm:p-8 border-t border-line-soft bg-surface-2">
        {/* The total, live. It has to be visible while dragging, because it is
            the thing being maintained and the user is not asked to trust that it
            is. */}
        <p
          className={`text-sm font-semibold ${problem ? "text-danger-fg" : "text-fg-muted"}`}
          role="status"
        >
          Total: {total.toFixed(2)} of 1.00
        </p>

        {problem && (
          <p className="text-sm text-danger-fg mt-2 flex items-start gap-1.5">
            <AlertTriangle size={14} className="shrink-0 mt-0.5" aria-hidden="true" />
            {problem}
          </p>
        )}

        <div className="mt-4 flex flex-col sm:flex-row sm:items-center gap-3">
          <Button
            variant="primary"
            onClick={submit}
            disabled={disabled || isRerunning || Boolean(problem) || !changed}
            loading={isRerunning}
          >
            {isRerunning && (
              <Loader2 size={16} className="mr-2 animate-spin" aria-hidden="true" />
            )}
            {isRerunning ? "Re-running" : "Re-run with these weights"}
          </Button>

          <p className="text-xs text-fg-muted max-w-prose">
            {changed
              ? "This replaces the current shortlist. Nobody has been contacted, so nothing is sent."
              : "Move a slider to re-run with different weights."}
          </p>
        </div>
      </footer>
    </section>
  );
}
