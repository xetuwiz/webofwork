/**
 * Presentation helpers for workflow state.
 *
 * Pure functions, no JSX, so the mapping from a status to what a person is told
 * is testable on its own. That mapping is a product decision wearing a
 * component's clothes: "PendingClientApproval" is the same string whether it is
 * on a badge, a button's disabled state, or a paragraph.
 */

/** Terminal states. A run here will never change again. */
export const TERMINAL_STATUSES = new Set(["Completed", "Failed", "Cancelled", "Rejected"]);

/**
 * Whether a run has stopped moving, whatever casing the status arrived in.
 *
 * <p>
 * Prefer this to <c>TERMINAL_STATUSES.has(status)</c>. The set holds the C#
 * spellings, so a direct lookup misses a snake_case status from the wire and a
 * finished run is polled forever -- which is exactly what happened. Four call
 * sites were relying on the query boundary having already normalised the value,
 * an invariant nothing in their own signatures enforced.
 *
 * @param {unknown} status
 * @returns {boolean}
 */
export const isTerminalStatus = (status) =>
  TERMINAL_STATUSES.has(normaliseWorkflowStatus(status));

/**
 * What a run means, in a client's words.
 *
 * The status names are .NET enum names -- the server sends them verbatim -- so
 * they are mapped rather than shown raw. `PendingClientApproval` means nothing to
 * someone being asked to approve a shortlist.
 */
const STATUS_COPY = {
  Pending: { label: "Starting", tone: "neutral", blurb: "Getting ready to run." },
  Running: { label: "Working", tone: "info", blurb: "The AI is reading and scoring." },
  PendingApproval: { label: "Awaiting review", tone: "warn", blurb: "Waiting for a decision." },
  PendingClientApproval: {
    label: "Needs your approval",
    tone: "warn",
    blurb: "A shortlist is ready. Nobody has been contacted yet.",
  },
  PendingManagerApproval: {
    label: "Awaiting manager approval",
    tone: "warn",
    blurb: "An admin signs off before any contract is created.",
  },
  ValidationFailed: { label: "Blocked", tone: "danger", blurb: "A safety check failed." },
  Approved: { label: "Approved", tone: "ok", blurb: "Approved." },
  Rejected: { label: "Rejected", tone: "danger", blurb: "Rejected." },
  Completed: { label: "Done", tone: "ok", blurb: "Finished." },
  Failed: { label: "Failed", tone: "danger", blurb: "Something went wrong. Nothing was sent." },
  Cancelled: { label: "Cancelled", tone: "neutral", blurb: "Stopped." },
};

/**
 * Describe a status.
 *
 * Falls back to showing the raw string rather than hiding it: an unrecognised
 * status is new server behaviour, and the client saying "Unknown" would make it
 * look like the server had nothing to say.
 */
export function describeStatus(status) {
  if (!status) return { label: "Unknown", tone: "neutral", blurb: "" };
  const normalised = normaliseWorkflowStatus(status);
  return (
    STATUS_COPY[normalised] ?? {
      // Not "this page is stale". The status is one this build has no copy for,
      // which is a different thing and has its own wording.
      label: String(status),
      tone: "neutral",
      blurb: "This status is not one this page knows about.",
    }
  );
}

/**
 * The .NET enum name for a status, whatever casing the wire used.
 *
 * ## Why this is needed
 *
 * `Program.cs` registers `JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower)`
 * globally, so every status arrives as `pending_client_approval`. Everything below
 * this function compares against the C# spelling, `PendingClientApproval`.
 *
 * Nothing reconciled the two, and every consumer silently took the wrong branch:
 *
 *  - `describeStatus` missed `STATUS_COPY` and fell through to its default, which
 *    renders the raw string as the label and "This state is newer than this page."
 *    as the blurb. That blurb was never about staleness -- it was the
 *    unrecognised-status message, and it appeared on every run.
 *  - `canApproveShortlist` was permanently false, so **the approve control never
 *    rendered at all**. A finished shortlist sat there with no way to act on it.
 *  - `TERMINAL_STATUSES` never matched, so the page kept polling a run that had
 *    finished.
 *  - `pollIntervalFor` polled statuses it did not recognise.
 *  - the "this run failed" notice and the panel's disabled state never fired.
 *
 * Applied once at the query boundary rather than inside each consumer, so the
 * casing is settled in one place and `STATUS_COPY` can stay readable.
 *
 * @param {unknown} raw
 * @returns {string | null | undefined} the C# spelling when it is recognised;
 *   otherwise `raw` unchanged, so an unknown status is still displayed rather
 *   than silently blanked.
 */
export function normaliseWorkflowStatus(raw) {
  if (typeof raw !== "string" || raw.length === 0) return raw;

  // Already the C# spelling. Checked before transforming so a status that is
  // genuinely spelled this way is not round-tripped through the snake_case path.
  if (STATUS_COPY[raw]) return raw;

  const pascal = raw
    .split(/[_\s-]+/)
    .filter(Boolean)
    .map((part) => part.charAt(0).toUpperCase() + part.slice(1))
    .join("");

  return STATUS_COPY[pascal] ? pascal : raw;
}

/** Whether the client can act on this run right now. */
export const canApproveShortlist = (status) =>
  normaliseWorkflowStatus(status) === "PendingClientApproval";

/** Whether an admin can act on this run right now. */
export const canApproveContract = (status) => {
  const normalised = normaliseWorkflowStatus(status);
  return normalised === "PendingManagerApproval" || normalised === "PendingApproval";
};

/** Whether the approve control should be offered at all. */
export const hasShortlist = (shortlist) => Array.isArray(shortlist) && shortlist.length > 0;

/**
 * Present a score as a percentage.
 *
 * The server sends 0-100 for match scores, which is not the same as a
 * proportion, and a screen that divides it by 100 again shows 0.4%. Guarded so a
 * missing score renders as an em dash rather than `NaN%`.
 */
export function formatScore(score) {
  if (score === null || score === undefined || Number.isNaN(Number(score))) return "—";
  const value = Number(score);
  if (value > 0 && value <= 1) return `${Math.round(value * 100)}%`;
  return `${Math.round(value)}%`;
}

/**
 * A stable key for a shortlist row.
 *
 * The freelancer id, not the array index: the list is sorted by score, so an
 * index key would remount a row when the order changed and swallow a click
 * mid-interaction.
 */
export const shortlistKey = (row) => row?.freelancerId ?? row?.id ?? String(row);

// ── the scoring weights ────────────────────────────────────────────────

/**
 * The five factors, in the order they are shown.
 *
 * The same five the planner produces, so adjusting a weight is adjusting the
 * planner's plan rather than setting a parallel set of numbers the matcher has
 * never heard of. Order is stable because the sliders are rendered in a fixed
 * order and a reordered list under a moving cursor is how the wrong number gets
 * dragged.
 */
export const WEIGHT_FACTORS = [
  { key: "skill_coverage", label: "Skill coverage", hint: "How well their skills match what the project needs." },
  { key: "portfolio_relevance", label: "Relevant past work", hint: "Whether their portfolio resembles this kind of work." },
  { key: "past_success_rate", label: "Past success rate", hint: "How reliably they have completed contracts before." },
  { key: "availability_and_speed", label: "Availability", hint: "Whether they can start soon, and how fast they deliver." },
  { key: "budget_fit", label: "Budget fit", hint: "Whether their prices suit what you have set aside." },
];

/**
 * The server's defaults, used before a plan has been read.
 *
 * These must match `MatchPlanDto` on the .NET side. If they drift, the sliders
 * open on numbers the server would not accept, and the first save is a 400 the
 * client cannot explain.
 */
export const DEFAULT_WEIGHTS = {
  skill_coverage: 0.35,
  portfolio_relevance: 0.2,
  past_success_rate: 0.2,
  availability_and_speed: 0.15,
  budget_fit: 0.1,
};

export const DEFAULT_SHORTLIST_SIZE = 10;

/**
 * Read a plan out of whatever the shortlist endpoint returned.
 *
 * The server sends the plan as a JSON *string*, because `WorkflowRun.Plan` is
 * jsonb held as text. So this handles both shapes: a string from the server, and
 * an object in case that ever changes. Falling back to the defaults means the
 * sliders always have something to render.
 *
 * @returns {{weights: object, shortlistSize: number, fromServer: boolean}}
 */
export function readPlan(plan) {
  const fallback = {
    weights: { ...DEFAULT_WEIGHTS },
    shortlistSize: DEFAULT_SHORTLIST_SIZE,
    fromServer: false,
  };

  if (!plan) return fallback;

  let parsed = plan;
  if (typeof plan === "string") {
    try {
      parsed = JSON.parse(plan);
    } catch {
      // A plan that will not parse is a run whose weights display steps aside
      // for. Defaults are better than an empty panel, and the server has
      // already scored the shortlist either way.
      return fallback;
    }
  }

  // An array is an object, so `typeof` alone would let `"[1,2]"` through as a
  // plan with no weights -- reported as `fromServer: true` with every slider
  // silently at its default, which looks like the server chose those numbers.
  if (!parsed || typeof parsed !== "object" || Array.isArray(parsed)) return fallback;

  const weights = { ...DEFAULT_WEIGHTS };
  for (const { key } of WEIGHT_FACTORS) {
    const value = Number(parsed.weights?.[key]);
    if (Number.isFinite(value)) weights[key] = value;
  }

  const size = Number(parsed.shortlist_size ?? parsed.shortlistSize);
  const shortlistSize = Number.isFinite(size) ? size : DEFAULT_SHORTLIST_SIZE;

  return { weights, shortlistSize, fromServer: true };
}

/** The weights as they add up. Shown live, because they must total 1. */
export const totalWeight = (weights) =>
  WEIGHT_FACTORS.reduce((sum, { key }) => sum + (Number(weights?.[key]) || 0), 0);

/**
 * Why a set of weights cannot be submitted, or null.
 *
 * The same rules the server enforces, checked here so the button is disabled
 * with a reason beside it rather than the user pressing it and getting a 400.
 * The server re-checks regardless -- this is a convenience, not the guarantee.
 */
export function planProblem(weights, shortlistSize) {
  for (const { key, label } of WEIGHT_FACTORS) {
    const value = Number(weights?.[key]);
    if (!Number.isFinite(value) || value < 0 || value > 1) {
      return `${label} must be between 0 and 1.`;
    }
  }

  const total = totalWeight(weights);
  if (Math.abs(total - 1) > 0.001) {
    return `These add up to ${total.toFixed(2)}, not 1. They are relative, so the total decides the scale of every score.`;
  }

  if (!Number.isInteger(shortlistSize) || shortlistSize < 5 || shortlistSize > 15) {
    return "The shortlist size must be between 5 and 15.";
  }

  return null;
}

/**
 * Rescale weights so they add to 1, moving only the factor being dragged.
 *
 * The obvious alternative -- dividing every weight by the total -- makes the
 * slider the user is holding jump, because all five move at once. Holding one
 * fixed and absorbing the difference in the others is the only version where
 * the control under the cursor stays put.
 *
 * @param {string} pinnedKey the factor the user is adjusting
 */
export function normaliseAround(weights, pinnedKey, pinnedValue) {
  const others = WEIGHT_FACTORS.filter(({ key }) => key !== pinnedKey);
  const remaining = 1 - pinnedValue;

  const currentOthers = others.reduce(
    (sum, { key }) => sum + (Number(weights?.[key]) || 0),
    0,
  );

  const next = { [pinnedKey]: pinnedValue };

  if (currentOthers <= 0) {
    // Everything else is at zero, so there is nothing to absorb the remainder
    // proportionally. Split it evenly rather than assigning it all to one
    // factor, which would be a larger change than the drag asked for.
    for (const { key } of others) next[key] = remaining / others.length;
  } else {
    for (const { key } of others) {
      next[key] = ((Number(weights?.[key]) || 0) / currentOthers) * remaining;
    }
  }

  // Rounding to four places keeps the sum honest. Without it, six factors each
  // rounded from 1/6 accumulate a visible drift and the plan is refused for
  // summing to 0.999.
  for (const { key } of WEIGHT_FACTORS) {
    next[key] = Math.round(next[key] * 10000) / 10000;
  }

  return next;
}
