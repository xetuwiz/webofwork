/**
 * What the weights panel says about where its numbers came from.
 *
 * ## Why this is a module and not a line inside the panel
 *
 * It is pure, it has four distinguishable cases, and the case that matters most
 * is the one that must never be reached by accident. Exported from the `.jsx` it
 * would also have broken `react-refresh/only-export-components`, since a module
 * exporting both a component and a function hot-reloads as neither.
 *
 * ## The failure this exists for
 *
 * The shortlist page rendered the panel's defaults and captioned them "The AI has
 * not chosen its weights yet. These are the defaults it starts from."
 *
 * The AI had chosen them. Run 23's planning step succeeded and wrote
 * `skill_coverage 0.4`, `portfolio_relevance 0.25` and the rest into the step log.
 * The endpoint read `WorkflowRun.Plan`, which is written only when the *client*
 * supplies a plan -- null on every first run -- so `fromServer` was false and the
 * panel said nothing had been chosen.
 *
 * That caption is not cosmetic. It tells the client the scores below it came from
 * nowhere, which is the one thing this panel exists to prevent: a number with no
 * stated provenance is a number nobody should act on.
 */

/**
 * @param {{ fromServer: boolean }} initial
 *   What `readPlan` made of the plan the server sent. `false` means no usable
 *   weights arrived.
 * @param {string | null | undefined} planSource
 *   The server's own attribution: `"planner"`, `"supplied"`, or absent.
 * @returns {string}
 */
export function weightCaption({ fromServer }, planSource) {
  // `fromServer` is checked first and wins. A source arriving without a plan is
  // inconsistent, and "no weights came back" is the honest reading of it -- the
  // alternative is to narrate a provenance for sliders that were never set.
  if (!fromServer) {
    return "The AI has not chosen its weights yet. These are the defaults it starts from.";
  }

  if (planSource === "supplied") {
    return "These are the weights you set for this shortlist.";
  }

  if (planSource === "planner") {
    return "These are the weights the AI chose for this shortlist.";
  }

  // A plan with no source, or one this panel does not recognise. Say only what is
  // true of it: the weights are real, their origin is not stated here. Guessing
  // would be the same dishonesty in the other direction.
  return "These are the weights this shortlist was produced with.";
}