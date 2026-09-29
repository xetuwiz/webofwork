import { useEffect, useState } from 'react';

/**
 * Debounce a value.
 *
 * Every search box that talks to the API used to fire a request per keystroke.
 * Typing "webofwork" into an eight-character field is seven requests, six of
 * which are results nobody looks at, and with `keepPreviousData` the list
 * visibly flickers as responses land out of order.
 *
 * Client-side filtering (the category picker, the chat room list) deliberately
 * does *not* use this -- filtering an array already in memory is instant, and
 * adding a delay would just feel broken.
 *
 * The empty case short-circuits without touching state. That is both a UX
 * decision (clearing a field should be instant, never debounced) and the reason
 * this returns a derived value instead of setting state in the effect body:
 * a synchronous setState in an effect is a cascading render.
 */
export const useDebounce = (value, delay = 350) => {
  const [debounced, setDebounced] = useState(value);

  const isEmpty = value === '' || value === null || value === undefined;

  useEffect(() => {
    if (isEmpty) return undefined;
    const timer = setTimeout(() => setDebounced(value), delay);
    // Cancel on every keystroke, so only the last one wins.
    return () => clearTimeout(timer);
  }, [value, delay, isEmpty]);

  return isEmpty ? value : debounced;
};

/**
 * Like {@link useDebounce}, but also reports whether a value is still waiting to
 * be committed.
 *
 * That flag is what lets a search box show "still working…" instead of leaving the
 * user staring at results for a term they have already finished typing, with no
 * way to tell whether the field is responding.
 *
 * `isPending` is derived from `value !== debounced` rather than stored in its own
 * state. Two pieces of state tracking one fact can disagree, and a spinner that
 * keeps spinning after the results have arrived is worse than no spinner.
 *
 * @returns {[*, boolean]} `[debouncedValue, isPending]`
 */
export const useDebouncedValue = (value, delay = 350) => {
  const [debounced, setDebounced] = useState(value);

  const isEmpty = value === '' || value === null || value === undefined;

  useEffect(() => {
    if (isEmpty) return undefined;
    const timer = setTimeout(() => setDebounced(value), delay);
    return () => clearTimeout(timer);
  }, [value, delay, isEmpty]);

  const committed = isEmpty ? value : debounced;
  return [committed, !isEmpty && value !== committed];
};

export default useDebounce;
