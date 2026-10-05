/**
 * Turns anything that was thrown into a sentence worth showing a user.
 *
 * Before this there were 16 different ways of pulling a message out of an axios
 * error across the app, all of them partially correct:
 *
 *   error.response?.data?.error   (x17)   the convention most endpoints use
 *   error.response?.data?.detail  (x8)    ASP.NET ProblemDetails
 *   error.response?.data?.message (x7)    some handlers
 *   error.response?.data?.[0]     (x2)    array-of-strings validators
 *
 * A call site that guessed the wrong one showed a raw fallback like "Something
 * went wrong" for a 400 that had a perfectly good message, and a call site that
 * guessed right still produced nothing useful for a validation error, because
 * the server puts those in a `errors` dictionary keyed by field name:
 *
 *   { "title": "Validation failed", "errors": { "Email": ["Already in use."] } }
 *
 * This walks all of those shapes in order of specificity, so the message the
 * user sees is the message the server actually sent.
 */

/** Turn a FluentValidation `errors` dictionary into one readable line. */
const fromValidationErrors = (errors) => {
  if (!errors || typeof errors !== 'object' || Array.isArray(errors)) return null;

  const parts = [];
  for (const [field, messages] of Object.entries(errors)) {
    if (!Array.isArray(messages)) continue;
    // The API uses PascalCase field names; "Email" reads better than "email"
    // in a sentence, so leave the casing alone but strip any trailing noise.
    const label = field.replace(/([a-z])([A-Z])/g, '$1 $2');
    const text = messages.filter(Boolean).join(' ');
    if (text) parts.push(`${label}: ${text}`);
  }
  return parts.length ? parts.join('. ') : null;
};

/** Turn an array of strings or `{ message }` objects into one line. */
const fromArray = (value) => {
  if (!Array.isArray(value)) return null;
  const parts = value
    .map((item) => (typeof item === 'string' ? item : item?.message || item?.detail))
    .filter(Boolean);
  return parts.length ? parts.join('. ') : null;
};

const isUsable = (value) => typeof value === 'string' && value.trim().length > 0;

/**
 * @param {unknown} error   The rejected value from axios, react-query, or a try/catch.
 * @param {string}  fallback Sentence to use when the error carries nothing useful.
 */
export const getErrorMessage = (error, fallback = 'Something went wrong. Please try again.') => {
  if (!error) return fallback;

  // Already a plain string, e.g. `throw new Error(...)` normalised away.
  if (isUsable(error)) return error.trim();

  const data = error?.response?.data;

  // Field-level validation is the most specific thing the server can say.
  const validation = fromValidationErrors(data?.errors);
  if (validation) return validation;

  const arrayMessage = fromArray(data);
  if (arrayMessage) return arrayMessage;

  // The three single-message conventions, most likely first.
  for (const key of ['error', 'detail', 'message']) {
    if (isUsable(data?.[key])) return data[key].trim();
  }

  // ProblemDetails with only a title (e.g. 404 "Not Found").
  if (isUsable(data?.title)) {
    return isUsable(data?.status) ? `${data.title} (${data.status})` : data.title;
  }

  // Some endpoints nest the payload one level deeper.
  if (data && typeof data === 'object') {
    for (const key of ['error', 'detail', 'message']) {
      if (isUsable(data[key])) return data[key].trim();
    }
  }

  // No response at all means the request never reached the server. Distinguish
  // that from a rejected request, because the fix is different.
  if (error?.response === undefined) {
    if (typeof navigator !== 'undefined' && navigator.onLine === false) {
      return "You appear to be offline. Check your connection and try again.";
    }
    if (error?.message === 'Network Error' || /network|fetch|timeout/i.test(error?.message || '')) {
      return "Can't reach the server. Check your connection and try again.";
    }
  }

  if (isUsable(error?.message) && !/Request failed with status code/i.test(error.message)) {
    return error.message.trim();
  }

  return fallback;
};

/**
 * A short, human summary of a failure, for when the detail is too long or too
 * technical to put in front of a user but the failure still needs a name --
 * page titles, error-boundary headings, log lines.
 */
export const getErrorTitle = (error) => {
  const status = error?.response?.status;
  if (status === 401) return 'Please sign in again';
  if (status === 403) return "You don't have access to this";
  if (status === 404) return "We couldn't find that";
  if (status === 409) return 'That conflicts with something else';
  if (status === 422) return 'Some details need fixing';
  if (status === 429) return 'Too many requests';
  if (status >= 500) return 'The server had a problem';
  if (status >= 400) return "That didn't work";
  if (typeof navigator !== 'undefined' && navigator.onLine === false) return "You're offline";
  return 'Something went wrong';
};
