import { useEffect } from "react";
import { useFormState } from "react-hook-form";

/**
 * Warn before losing unsaved form changes on a page unload.
 *
 * ## Why
 *
 * `AddProject.jsx` and `EditProject.jsx` each had their own hand-rolled
 * `beforeunload` listener that fired **unconditionally** -- on every unload,
 * even with a completely untouched form. A user who opened "Post a project",
 * decided against it, and clicked a nav link got "Leave site? Changes you made
 * may not be saved."
 *
 * That is worse than having no guard: a prompt that fires when there is nothing
 * to lose trains people to dismiss it reflexively, which is precisely the reflex
 * you do not want the one time someone has typed forty minutes into a form.
 *
 * ## What this covers, and what it deliberately does not
 *
 * Covered: tab close, refresh, and navigating to another site. The browser owns
 * that dialog and its wording; a custom string is ignored by modern browsers.
 *
 * **Not** covered: in-app navigation. Clicking a `<Link>` does not unload the
 * document, so `beforeunload` never fires and the form is discarded silently.
 * The correct fix is React Router's `useBlocker`, but that requires a *data
 * router* -- and `routes/AppRoutes.jsx` mounts a plain `<BrowserRouter>`, where
 * `useBlocker` throws "useBlocker must be used within a data router".
 *
 * So wiring it up means migrating the app to `createBrowserRouter`, which
 * restructures every route and interacts with the modal system. That is a
 * separate piece of work, not something to smuggle in here. Until it happens,
 * in-app navigation is a known gap -- see the note in the module below rather
 * than a silent one.
 *
 * ## Dirty detection
 *
 * react-hook-form's `isDirty`, so the guard tracks the form itself rather than
 * a hand-maintained "has the user typed" flag that would drift. A successful
 * submit resets the form, `isDirty` goes false, and the guard goes quiet by
 * itself -- no `submitted` flag to keep in sync.
 */
export function useUnsavedChangesGuard({ isDirty, message }) {
  useEffect(() => {
    if (!isDirty) return undefined;

    const handler = (event) => {
      event.preventDefault();
      // Legacy field, still honoured by Chrome and Safari. Modern browsers
      // ignore any custom text and show their own generic wording.
      event.returnValue = message;
      return message;
    };

    window.addEventListener("beforeunload", handler);
    return () => window.removeEventListener("beforeunload", handler);
  }, [isDirty, message]);
}

/**
 * Convenience wrapper for a react-hook-form `control`.
 *
 * Pass the `control` from `useForm(...)`; everything else is derived.
 *
 * @param {object} control from `useForm()`
 * @param {string} [message]
 */
export function useFormUnsavedChangesGuard(control, message) {
  const { isDirty, isSubmitting } = useFormState({ control });

  useUnsavedChangesGuard({
    // `isSubmitting` is excluded so the guard lifts the moment a submit starts,
    // otherwise a successful submit that navigates would trip its own prompt.
    isDirty: isDirty && !isSubmitting,
    message: message ?? "You have unsaved changes.",
  });
}
