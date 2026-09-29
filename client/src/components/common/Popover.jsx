/**
 * Popover: an anchored panel for filter menus and account dropdowns.
 *
 * ## Why
 *
 * Six dropdowns were hand-rolled as a `useState` boolean plus a conditionally
 * rendered absolutely-positioned `<div>`:
 *
 *  * `FindJobs.jsx` — category, skill, budget and delivery menus (4)
 *  * `AdminTransactions.jsx` — status menu
 *  * `ClientHeader.jsx` — the mobile nav
 *
 * Verified against the source: **none of the six handled Escape.** Two of them
 * did not even close on an outside click. So a keyboard user could open a menu
 * and then be unable to dismiss it with the keyboard — the only way out was to
 * guess where to click. None returned focus to the trigger either, so after
 * closing, focus fell to the top of the document and the user lost their place.
 *
 * That is not a styling inconsistency, it is a keyboard trap. A shared primitive
 * is the fix, exactly as it was for `<Dialog>`.
 *
 * ## Why Radix
 *
 * `@radix-ui/react-popover` supplies, per instance, without any of it being
 * written here: Escape to close, dismiss on outside pointer-down, focus moved
 * into the panel on open and **returned to the trigger on close**, arrow-key
 * navigation, `aria-expanded` / `aria-haspopup` on the trigger, and a content
 * `role="dialog"`.
 *
 * ## Anchoring
 *
 * Radix positions against the trigger with floating-ui, so the panel is placed
 * with inline `position: absolute/fixed` + `transform` on a wrapper it controls.
 * Do NOT also apply a Tailwind `absolute`/`top-*` to `<PopoverContent>` — that
 * fights the positioning engine and produces a panel in the wrong place on
 * scroll. The props below are for look, not for placement.
 */
import * as RadixPopover from "@radix-ui/react-popover";
import { forwardRef } from "react";

/** Matches the `CONTROL` surface language used by Field/Input. */
const PANEL =
  "z-popover min-w-[12rem] overflow-hidden rounded-xl border border-line bg-surface " +
  "p-1 shadow-xl";

const ITEM =
  "flex w-full items-center gap-2 rounded-lg px-3 py-2 text-left text-sm font-medium " +
  "text-fg-muted transition-colors hover:bg-surface-2 hover:text-fg " +
  "focus-visible:outline-none focus-visible:bg-surface-2 focus-visible:text-fg " +
  "disabled:pointer-events-none disabled:opacity-40";

const ITEM_DANGER =
  "flex w-full items-center gap-2 rounded-lg px-3 py-2 text-left text-sm font-medium " +
  "text-danger transition-colors hover:bg-danger-soft " +
  "focus-visible:outline-none focus-visible:bg-danger-soft " +
  "disabled:pointer-events-none disabled:opacity-40";

/**
 * `align` is start/center/end, `side` picks the side. Radix flips automatically
 * when there is not room, so "bottom" usually means "bottom, or top if it would
 * overflow".
 */
export const Popover = RadixPopover.Root;

export const PopoverTrigger = RadixPopover.Trigger;

/**
 * Anchor the panel to something other than its trigger.
 *
 * Needed for a full-width menu: `ClientHeader`'s mobile nav hangs off the whole
 * header row, not the hamburger, so anchoring to the trigger would put a
 * button-width panel under a button.
 */
export const PopoverAnchor = RadixPopover.Anchor;

/**
 * Dropdown body.
 *
 * `aria-label` is required here even though the panel already has
 * `role="dialog"`: a dialog is announced by its heading, and these panels are
 * labelled by the trigger's own text, which is not inside the panel.
 */
export const PopoverContent = forwardRef(function PopoverContent(
  { className = "", align = "start", sideOffset = 6, "aria-label": ariaLabel, children, ...rest },
  ref,
) {
  return (
    <RadixPopover.Portal>
      <RadixPopover.Content
        ref={ref}
        align={align}
        sideOffset={sideOffset}
        aria-label={ariaLabel}
        // Same reasoning as Dialog: Radix emits role="dialog" but the panel must
        // not be reachable by the virtual cursor while a menu is open.
        className={`${PANEL} animate-in fade-in zoom-in-95 duration-fast ${className}`}
        {...rest}
      >
        {children}
      </RadixPopover.Content>
    </RadixPopover.Portal>
  );
});

/** A single selectable row. Use a real `<button>`; Radix gives it the roles. */
export function PopoverItem({ className = "", danger = false, ...rest }) {
  return <button type="button" className={`${danger ? ITEM_DANGER : ITEM} ${className}`} {...rest} />;
}

/** Non-interactive separator, so the group reads as one menu. */
export function PopoverSeparator({ className = "" }) {
  return <div role="separator" className={`my-1 h-px bg-line-soft ${className}`} />;
}

/** Small heading, for a panel that groups several sections. */
export function PopoverLabel({ className = "", children, ...rest }) {
  return (
    <div className={`px-3 py-1.5 text-[10px] font-bold uppercase tracking-widest text-fg-subtle ${className}`} {...rest}>
      {children}
    </div>
  );
}

export default Popover;
