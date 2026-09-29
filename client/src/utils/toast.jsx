import { toast } from 'react-toastify';
import { CheckCircle2, AlertTriangle, Info, X } from 'lucide-react';
import { getErrorMessage } from './errorMessage';

/**
 * Toast helpers.
 *
 * ## Why this imports the *styled* build, after `unstyled` was tried first
 *
 * The app originally rendered react-toastify's stock appearance, which is a
 * light box with hardcoded colours that cannot read the app's tokens. The fix
 * attempted was `react-toastify/unstyled` -- no stylesheet, appearance built from
 * our own semantic classes.
 *
 * That does not work, and the reason is structural rather than cosmetic:
 *
 *  - `autoClose` is **not** timer-driven. The library calls `closeToast` from an
 *    `onAnimationEnd` handler, so the `Toastify__trackProgress` keyframe is
 *    load-bearing. Without it, toasts never dismiss at all.
 *  - `.Toastify__toast` supplies `position: relative`, which the progress bar
 *    needs as its containing block. Hand-rolling that is easy to get subtly
 *    wrong, and a missing it makes the bar size itself against the viewport.
 *  - `stacked` depends on a whole set of rules for collapse, `translate3d` and
 *    scale transforms. Reproducing those is not "theming", it is reimplementing
 *    the library.
 *
 * Each of those was patched individually here and each patch broke something
 * else. The supported route is to load the library's stylesheet and *theme it*,
 * which it is explicitly built for: `ReactToastify.css` exposes 41
 * `--toastify-*` custom properties, and `index.css` overrides them against the
 * app's tokens. Structural behaviour stays the library's responsibility;
 * appearance becomes ours.
 *
 * (v11 has no `containerClassName` or `bodyClassName` -- those were v10 props.)
 * The class hooks are `className`, `toastClassName` and `progressClassName`.
 *
 * The helpers live here rather than beside the `<ToastContainer>` component
 * because react-refresh only preserves state when a module exports components
 * and nothing else.
 *
 * Accessibility: `role` is set per toast type, so a success confirmation is
 * announced politely while a failure is announced assertively. A screen-reader
 * user otherwise gets no signal that a save failed.
 */

const ICONS = {
  success: CheckCircle2,
  error: AlertTriangle,
  warning: AlertTriangle,
  info: Info,
};

/**
 * Per-type classes applied by `toastClassName`.
 *
 * Only *layout* lives here -- the background, foreground and border colours do
 * not, and that is deliberate. react-toastify paints every toast with its own
 * unlayered rule (`.Toastify__toast-theme--light { background: ... }`), and an
 * unlayered rule beats a `@layer`ed Tailwind utility regardless of specificity.
 * So `bg-success-soft` here was silently losing to the library's white, and the
 * toasts rendered unthemed.
 *
 * The colours are therefore set in `index.css`, unlayered and keyed on the
 * library's own `Toastify__toast--<type>` class, beside the tokens they use.
 * One source of truth, and no cascade fight.
 */
const TONE = {
  success: 'border',
  error: 'border',
  warning: 'border',
  info: 'border',
};

const BAR = {
  success: 'bg-success',
  error: 'bg-danger',
  warning: 'bg-warning',
  info: 'bg-brand',
};

/** The toast body: icon, message, and a close affordance. */
const renderContent = (message, type, closeToast) => {
  const Icon = ICONS[type] || Info;
  return (
    <div className="flex w-full items-start gap-2.5 py-3 pl-3.5 pr-2">
      <Icon className="mt-0.5 h-4 w-4 shrink-0" aria-hidden="true" />
      <p className="min-w-0 flex-1 text-sm font-medium leading-relaxed">{message}</p>
      <button
        type="button"
        onClick={closeToast}
        aria-label="Dismiss notification"
        className="-mr-0.5 shrink-0 rounded-md p-1 opacity-60 transition-opacity hover:opacity-100 focus-visible:opacity-100 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-current"
      >
        <X className="h-3.5 w-3.5" aria-hidden="true" />
      </button>
    </div>
  );
};

/** Shared options so every entry point below behaves identically. */
const optionsFor = (type, overrides = {}) => ({
  type,
  // Errors linger: a failure usually needs reading and acting on, whereas a
  // success is just confirmation and should get out of the way.
  autoClose: type === 'error' ? 7000 : 3500,
  closeOnClick: false,
  pauseOnHover: true,
  // Off for the same reason as in AppToaster: react-toastify pauses on window
  // blur and resumes only on window focus, so having DevTools open held every
  // toast on screen indefinitely. It was set here as well as on the container,
  // and the container prop does not override a per-toast option -- the toast's
  // own value wins, so this line is the one that actually caused it.
  pauseOnFocusLoss: false,
  draggable: 'x',
  role: type === 'error' ? 'alert' : 'status',
  // Same contract as `progressClassName`: the return value replaces the
  // library's classes, so `defaultClassName` has to be spread in. The toast body
  // gets `relative` from here because `.Toastify__progress-bar` is absolutely
  // positioned against it, and the height has to be allowed to size to content.
  toastClassName: ({ defaultClassName }) =>
    `${defaultClassName} pointer-events-auto relative w-full overflow-hidden rounded-xl border shadow-lg backdrop-blur-md ${TONE[type] || TONE.info}`,
  // `progressClassName` is called with `{ rtl, type, defaultClassName }`, and
  // **its return value replaces the library's class list outright** -- the
  // default is offered as an argument, not merged in for you.
  //
  // Returning just `h-0.5 bg-success` therefore threw away
  // `Toastify__progress-bar--animated`, which is the only class that references
  // the trackProgress keyframe. With no animation, `onAnimationEnd` never fired,
  // and react-toastify dismisses toasts *from that event* rather than from a
  // timer -- so `autoClose` was inert and every toast stayed up forever.
  //
  // The fix is to spread `defaultClassName` first, then add our own. Same trap
  // applies to `toastClassName` below.
  progressClassName: ({ defaultClassName }) => `${defaultClassName} h-0.5 ${BAR[type] || BAR.info}`,
  icon: () => null,
  ...overrides,
});

const emit = (message, type, overrides) =>
  toast(
    (props) => renderContent(message, type, props.closeToast),
    optionsFor(type, overrides)
  );

export const toastSuccess = (message, overrides) => emit(message, 'success', overrides);
export const toastInfo = (message, overrides) => emit(message, 'info', overrides);
export const toastWarning = (message, overrides) => emit(message, 'warning', overrides);
export const toastError = (message, overrides) => emit(message, 'error', overrides);

/**
 * Toast an error object, pulling the real message out of whatever shape the
 * server used -- `error`, ProblemDetails `detail`, or a FluentValidation
 * `errors` dictionary.
 *
 * `fallback` should name the action that failed. "Something went wrong" tells
 * the user nothing about whether retrying is worth it; "Couldn't save your
 * profile" does.
 */
export const toastApiError = (error, fallback) =>
  toastError(getErrorMessage(error, fallback));

/**
 * Keeps one toast up for the duration of a long operation and resolves it to
 * success or error. Without this, clicking Save produces two seconds of apparent
 * nothing followed by an unrelated-looking message.
 */
export const toastPromise = (promise, { pending, success, error } = {}) => {
  const id = toast.loading(
    (props) => renderContent(pending || 'Working…', 'info', props.closeToast),
    optionsFor('info', {
      autoClose: false,
      isLoading: true,
      closeOnClick: false,
      draggable: false,
    })
  );

  promise
    .then(() => {
      toast.update(id, {
        ...optionsFor('success'),
        isLoading: false,
        render: (props) => renderContent(success || 'Done.', 'success', props.closeToast),
      });
    })
    .catch((err) => {
      toast.update(id, {
        ...optionsFor('error'),
        isLoading: false,
        autoClose: 7000,
        render: (props) =>
          renderContent(
            error || getErrorMessage(err, 'That did not work. Please try again.'),
            'error',
            props.closeToast
          ),
      });
    });

  return id;
};

export { renderContent, optionsFor };
