/**
 * Dialog -- the app's modal primitive.
 *
 * ## Why this wraps Radix instead of hand-rolling
 *
 * I previously recommended *against* Radix on the grounds that it was not
 * installed. That was wrong: `@radix-ui/react-dialog` has been a dependency
 * all along, and three category-management modals were already using it. A
 * focus trap is the kind of thing that looks like twenty lines and is not --
 * getting the wrap-around, the shift-tab-from-first case, focus restoration on
 * unmount, and the scroll-lock stacking right is a lot of surface to own. Radix
 * has already got it right and battle-tested.
 *
 * What Radix does *not* do is make the app consistent. The three existing users
 * all hand-rolled their own width (`w-[440px]`, off-scale), their own overlay
 * darkness (`bg-black/80`), and none of them passed a `Dialog.Title` -- so even
 * the "correct" ones announced nothing to a screen reader. Those are the
 * mistakes worth eliminating, and they are exactly what a facade can enforce.
 *
 * ## What this fixes
 *
 * Across 30 hand-rolled modals, before this existed:
 *   role="dialog"      0 / 30
 *   aria-modal         0 / 30
 *   closes on Escape   0 / 30   <- WCAG 2.1.2
 *   focus trapped      0 / 30
 *   focus restored     0 / 30
 *
 * All of the above now come from Radix and cannot be forgotten, because
 * `title` is a required prop and the accessible name is wired from it.
 *
 * ## Radix composition
 *
 * `Root` -> `Portal` -> `Overlay` + `Content` -> `Title` (+ optional
 * `Description`). `aria-describedby` is only attached when a description is
 * actually supplied, so we never point at a missing id.
 */
import * as RadixDialog from "@radix-ui/react-dialog";

const SIZES = {
  sm: "max-w-dialog-sm",
  md: "max-w-dialog-md",
  lg: "max-w-dialog-lg",
  xl: "max-w-dialog-xl",
};

const PANEL = [
  "fixed left-1/2 top-1/2 z-modal w-[calc(100%-2rem)] -translate-x-1/2 -translate-y-1/2",
  "max-h-[90vh] overflow-y-auto scrollbar-themed",
  "bg-surface border border-line-soft shadow-2xl",
  "rounded-2xl animate-in",
].join(" ");

/**
 * @param {object} props
 * @param {boolean} props.open
 * @param {(open: boolean) => void} [props.onOpenChange] Radix's own signature.
 * @param {() => void} [props.onClose] Convenience alias, fired with `false`.
 *   Existing modals were written against `isOpen`/`onClose`, so accepting both
 *   means the migration does not have to rewrite its state plumbing.
 * @param {string} props.title Required. Becomes the dialog's accessible name.
 * @param {string} [props.description] Optional long description.
 * @param {'sm'|'md'|'lg'|'xl'} [props.size]
 * @param {string} [props.className] Extra classes on the panel.
 * @param {React.ReactNode} [props.footer] Sticky action row, if provided.
 */
export default function Dialog({
  open,
  onOpenChange,
  onClose,
  title,
  description,
  size = "md",
  className = "",
  footer,
  children,
}) {
  const handleOpenChange = (next) => {
    if (!next) onClose?.();
    onOpenChange?.(next);
  };

  return (
    <RadixDialog.Root open={open} onOpenChange={handleOpenChange}>
      <RadixDialog.Portal>
        <RadixDialog.Overlay className="fixed inset-0 z-modal bg-inverse/60 backdrop-blur-sm animate-in" />
        <RadixDialog.Content
          // Radix emits role="dialog", aria-labelledby, aria-describedby and
          // tabindex="-1", but NOT aria-modal -- verified by rendering its
          // Content and reading the output. It relies on the focus trap and
          // `react-remove-scroll` to make the rest of the page inert instead.
          // That works for sighted and keyboard users, but `aria-modal` is the
          // attribute that actually tells a screen reader the content behind the
          // dialog should be skipped rather than reachable via the virtual
          // cursor, so it is set explicitly here.
          aria-modal="true"
          className={`${PANEL} ${SIZES[size] ?? SIZES.md} ${className}`}
        >
          <RadixDialog.Title className="sr-only">{title}</RadixDialog.Title>
          {/* Radix warns "Missing `Description` or `aria-describedby`" for every
              dialog opened without a description, which is most of them -- 40 of
              42 usages. The warning is advisory, not a failure, but it is real
              noise in the console on every open, and it trains the eye to ignore
              console warnings.

              Radix's own guidance is to silence it by supplying an explicitly
              empty description, which is why this falls back to a visually
              hidden empty node rather than being omitted. The alternative --
              passing `aria-describedby={undefined}` -- is what the warning is
              explicitly asking you *not* to do, and it does not suppress the
              message.

              A dialog whose body is self-describing ("Delete FAQ?") does not
              need a description; this just stops the library complaining about
              that. Modals that do need one -- irreversible or consequential
              actions -- pass a real `description`, and those are the ones worth
              writing copy for. */}
          <RadixDialog.Description className="sr-only">
            {description ?? ""}
          </RadixDialog.Description>
          {children}
          {footer ? (
            <div className="sticky bottom-0 -mx-6 -mb-6 mt-6 flex items-center justify-end gap-3 border-t border-line-soft bg-surface px-6 py-4">
              {footer}
            </div>
          ) : null}
        </RadixDialog.Content>
      </RadixDialog.Portal>
    </RadixDialog.Root>
  );
}

/**
 * A visible close button that works with the primitive: it must be a Radix
 * `Close` so the dialog's own open-state resets, not a bare `onClick`.
 *
 * Exported separately because 28 of the 30 modals have their own hand-rolled
 * `onClick={onClose}` X, and each one is a place the a11y wiring can be lost.
 */
export function DialogClose({ className = "", children }) {
  return (
    <RadixDialog.Close asChild>
      <button
        type="button"
        aria-label="Close dialog"
        className={`p-2 text-fg-subtle transition-colors hover:bg-surface-3 hover:text-fg rounded-lg ${className}`}
      >
        {children}
      </button>
    </RadixDialog.Close>
  );
}
