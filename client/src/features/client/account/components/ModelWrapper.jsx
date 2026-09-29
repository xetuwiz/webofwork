import Dialog from "../../../../components/common/Dialog";

/**
 * Shared shell for the account modals (sign in, sign up, forgot password, OTP).
 *
 * ## What this replaced
 *
 * This was a hand-rolled scrim: `fixed inset-0 bg-black/50 ... z-50` with a
 * click-to-close handler and nothing else. No `role="dialog"`, no `aria-modal`,
 * no Escape, no scroll lock, no focus trap -- the same four gaps the other 25
 * modals had, in one shared place.
 *
 * It now delegates to the `Dialog` primitive, so all four get the accessibility
 * behaviour from one implementation.
 *
 * ## Why `title` is passed in rather than read from the DOM
 *
 * The visible heading lives inside the child layouts (`SigninLayout` and
 * friends), so there is nothing here to point `aria-labelledby` at. The Dialog
 * primitive renders its own visually-hidden `Dialog.Title` from this prop, which
 * is why each caller has to supply a real one -- a dialog whose accessible name
 * is "Dialog" tells a screen-reader user nothing about what just opened.
 *
 * (The filename is misspelled: `ModelWrapper`, not `ModalWrapper`. Left as-is to
 * keep this change to the accessibility fix rather than churning four imports;
 * worth renaming separately.)
 */
/**
 * `size` defaults to `xl` rather than `md`.
 *
 * This wraps four modals, and two of them -- sign in and sign up -- render a
 * split panel whose banner takes 45% of the width. At the old `md` (600px) the
 * form column was 600 * 0.55 = ~330px, minus the form section's own padding, so
 * the inputs were roughly 260px wide. That reads as cramped on a desktop, and
 * it is the reason this was flagged.
 *
 * At `xl` (1100px) the banner keeps its proportion and the form column becomes
 * ~605px, which is a normal desktop form width. The banner proportion is
 * deliberately left alone rather than re-tuned: the split-panel look is the
 * point of these two screens, and shrinking the image to widen the form would
 * change the design rather than fix a sizing mistake.
 *
 * The other two consumers here -- forgot password and the OTP prompt -- are
 * single-column forms that are now wider than strictly necessary. That is the
 * cost of one shared default; they can pass `size="md"` if the extra width
 * starts to look wrong.
 */
export default function ModalWrapper({ children, onClose, title, description, size = "xl" }) {
  return (
    <Dialog open onClose={onClose} title={title} description={description} size={size}>
      {children}
    </Dialog>
  );
}
