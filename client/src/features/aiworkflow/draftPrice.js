/**
 * What the review panel shows about money.
 *
 * ## Why this is extracted
 *
 * The panel is the last thing a client reads before pressing "Fill the form".
 * Anything missing from it is a number they agree to without seeing.
 *
 * The live run showed `Price 700.0` for a proposal the form then filled as
 * $700 - $2,000. `fixed_price_usd ?? min_budget_usd ?? max_budget_usd` stopped
 * at the first defined value, so the *maximum* of a range was never displayed.
 * That is the figure a client is most likely to dispute and least likely to
 * notice was omitted.
 */

/**
 * @param {object} parsed  The draft's `raw`, already JSON-parsed.
 * @returns {string|null}  What to show, or null to show nothing.
 */
export function formatDraftPrice(parsed) {
  const money = (n) =>
    typeof n === "number" && Number.isFinite(n)
      ? n.toLocaleString("en-US", { maximumFractionDigits: 2 })
      : null;

  if (parsed?.pricing_type === "range") {
    const min = money(parsed.min_budget_usd);
    const max = money(parsed.max_budget_usd);
    // One bound without the other must say which, or a lone number reads as a
    // complete range and the client cannot tell it is half an answer.
    if (min && max) return `$${min} - $${max}`;
    if (min) return `$${min} and up (no maximum)`;
    if (max) return `up to $${max} (no minimum)`;
    return null;
  }

  const fixed = money(parsed?.fixed_price_usd);
  return fixed === null ? null : `$${fixed}`;
}

export default formatDraftPrice;
