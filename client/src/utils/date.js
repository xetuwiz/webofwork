/**
 * Date formatting helpers.
 *
 * Everything here is defensive on purpose. The app used to call
 * `new Date(value).toLocaleDateString()` inline at ~20 call sites, which renders
 * the literal string "Invalid Date" for a missing or wrongly-named field instead
 * of failing visibly. The contract deliverables list hit exactly that: the wire
 * field is `submitted_at` (`DeliverableResponse.SubmittedAt`), the client read
 * `created_at`, so `new Date(undefined).toLocaleDateString()` printed
 * "Invalid Date" beside every submitted file.
 *
 * `formatDateDMY` is the single formatter to reach for instead. It returns
 * [fallback] for anything that is not a real, parseable date, so a renamed or
 * missing field degrades to a quiet dash rather than leaking "Invalid Date"
 * into the UI.
 */

/**
 * @param {unknown} value        ISO string, epoch millis, or Date.
 * @param {string} [fallback]    Shown when the value is absent or unparseable.
 * @returns {string}
 */
export function formatDateDMY(value, fallback = "") {
  const date = toValidDate(value);
  if (!date) return fallback;
  return date.toLocaleDateString("en-GB", {
    day: "2-digit",
    month: "2-digit",
    year: "numeric",
  });
}

/**
 * Same as {@link formatDateDMY} plus the time of day, for timestamps where the
 * hour matters (dispute log, ticket history, audit lines).
 *
 * @param {unknown} value
 * @param {string} [fallback]
 * @returns {string}
 */
export function formatDateTimeDMY(value, fallback = "") {
  const date = toValidDate(value);
  if (!date) return fallback;
  return date.toLocaleString("en-GB", {
    day: "2-digit",
    month: "2-digit",
    year: "numeric",
    hour: "2-digit",
    minute: "2-digit",
  });
}

/**
 * `true` only for a value that yields a real date. `new Date(...)` is
 * `Invalid Date` -- not an exception -- for garbage input, so the only correct
 * test is `getTime()` being finite.
 *
 * @param {unknown} value
 * @returns {Date | null}
 */
export function toValidDate(value) {
  if (value == null || value === "") return null;
  const date = value instanceof Date ? value : new Date(value);
  return Number.isFinite(date.getTime()) ? date : null;
}
