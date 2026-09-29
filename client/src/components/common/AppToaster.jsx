import { ToastContainer } from 'react-toastify';

/**
 * The toast viewport. Mount once, near the root.
 *
 * Styling lives in ../utils/toast.jsx and is applied per toast through
 * `toastClassName` / `progressClassName`, against the app's semantic tokens.
 * That separation is deliberate: react-refresh only preserves component state
 * when a module exports components and nothing else, so the helpers and this
 * component cannot share a file.
 *
 * ## Why the container's own className is now empty
 *
 * It used to be a hand-rolled positioner:
 *
 *   "pointer-events-none fixed inset-x-0 top-0 z-toast mx-auto flex ..."
 *
 * That is `inset-x-0` + `mx-auto`, which is a **top-centre** layout -- it
 * stretched the container across the full viewport and centred it with auto
 * margins. It was fighting `position="top-right"` on the very next line, and it
 * won, because inline Tailwind utilities on the element beat a stylesheet rule.
 * That is why toasts were appearing centred at the top instead of in the
 * right-hand column.
 *
 * The `unstyled` build ships no CSS, so nothing was left to position the
 * container: the per-position rules (`--top-right`, `align-items: end`, the
 * safe-area insets) all live in `ReactToastify.css`, which is deliberately not
 * loaded. `../utils/toast.jsx` now supplies those four rules itself, so
 * `position` is honoured and the tokens stay ours.
 *
 * ## On `pauseOnFocusLoss` / `pauseOnHover`
 *
 * Both are also set in `optionsFor`, which is where they belong -- they are
 * per-toast behaviour. They are repeated here because `ToastContainer` props
 * act as defaults, and the helper is the source of truth. Pausing a toast holds
 * it open indefinitely by design, so this is a *behaviour* to be aware of when
 * testing: hovering a toast and then moving the pointer away must resume it.
 */
export default function AppToaster() {
  return (
    <ToastContainer
      className="toast-stack"
      position="top-right"
      newestOnTop
      stacked
      limit={4}
      draggablePercent={80}
      // NOTE: `pauseOnFocusLoss` is deliberately OFF here.
      //
      // It was on, and it is why a toast would sit forever while you had
      // DevTools open. The library implements it as:
      //
      //   document.hasFocus() || pause()     // on mount
      //   window 'blur'  -> pause
      //   window 'focus' -> resume            // the ONLY resume path
      //
      // So a toast raised while the window was unfocused paused immediately, and
      // only resumed when the window got focus back. Reading the console is
      // enough to trigger it. A paused toast is held open indefinitely, which is
      // also why it looked like `autoClose` was still broken.
      //
      // The trade-off: a backgrounded tab will now let toasts expire instead of
      // holding them. For a product with no long-running uploads, that is the
      // right way round -- the notification is stale by the time the user
      // returns, and a pile of expired toasts is worse than none.
      //
      // `pauseOnHover` stays on: that one is deliberate, discoverable, and
      // resumes on pointer-leave rather than waiting for a window event.
      pauseOnHover
      closeButton={false}
      hideProgressBar={false}
      // Alt+T moves focus to the newest notification.
      hotKeys={(e) => e.altKey && e.key === 't'}
      aria-label="Notifications"
    />
  );
}
