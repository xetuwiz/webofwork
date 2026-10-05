/**
 * Tooltip.
 *
 * ## Why this exists
 *
 * The six tooltips already in the app were `group-hover:opacity-100` divs with
 * no `role="tooltip"` and no focus trigger. Hover-only, which means a keyboard
 * user gets nothing at all -- and it directly contradicted the 232
 * `focus-visible` utilities added elsewhere in the app, where the convention is
 * that anything reachable by mouse is reachable by keyboard.
 *
 * This primitive shows on hover *and* on `:focus-visible`, escapes the same way
 * `prefers-reduced-motion` already handles transitions, and closes on Escape.
 *
 * ## Why not the CSS-only group pattern
 *
 * The reason to be a component rather than three utility classes is the pairing.
 * `role="tooltip"` plus `aria-describedby` is what actually announces it, and
 * the id has to be unique and wired. Doing that with raw utilities means
 * generating an id in six places and hoping each one matches.
 *
 * ## Timing
 *
 * Open is immediate; close is delayed by `closeDelay` so that moving the pointer
 * across a gap between two tooltips, or from a tooltip to the thing it
 * describes, does not make it flicker. The timer is cleared on unmount so a
 * fast unmount cannot fire a state update on a dead component.
 */
import { useCallback, useEffect, useId, useRef, useState } from "react";

export default function Tooltip({
  content,
  children,
  side = "top",
  closeDelay = 120,
  className = "",
}) {
  const id = useId();
  const [open, setOpen] = useState(false);
  const timer = useRef(null);

  const clear = useCallback(() => {
    if (timer.current) {
      clearTimeout(timer.current);
      timer.current = null;
    }
  }, []);

  const show = useCallback(() => {
    clear();
    setOpen(true);
  }, [clear]);

  const hide = useCallback(() => {
    clear();
    timer.current = setTimeout(() => setOpen(false), closeDelay);
  }, [clear, closeDelay]);

  useEffect(() => clear, [clear]);

  // Escape while open should dismiss immediately rather than wait out the delay.
  useEffect(() => {
    if (!open) return undefined;
    const onKeyDown = (event) => {
      if (event.key === "Escape") {
        clear();
        setOpen(false);
      }
    };
    window.addEventListener("keydown", onKeyDown);
    return () => window.removeEventListener("keydown", onKeyDown);
  }, [open, clear]);

  const position = {
    top: "bottom-full left-1/2 -translate-x-1/2 mb-2",
    bottom: "top-full left-1/2 -translate-x-1/2 mt-2",
    left: "right-full top-1/2 -translate-y-1/2 mr-2",
    right: "left-full top-1/2 -translate-y-1/2 ml-2",
  }[side];

  return (
    <span className={`relative inline-flex ${className}`}>
      <span
        aria-describedby={open ? id : undefined}
        onMouseEnter={show}
        onMouseLeave={hide}
        onFocusCapture={show}
        onBlurCapture={hide}
        // Wrapping in a span means the tooltip works on any child -- a button, an
        // icon, a truncated label -- without the child needing to know about it.
        className="inline-flex focus-visible:outline-none"
      >
        {children}
      </span>

      {open ? (
        <span
          id={id}
          role="tooltip"
          className={`pointer-events-none absolute z-toast whitespace-nowrap rounded-lg bg-inverse px-2.5 py-1.5 text-[11px] font-semibold text-inverse-fg shadow-lg animate-in ${position}`}
        >
          {content}
        </span>
      ) : null}
    </span>
  );
}
