/**
 * Button -- the app's only button.
 *
 * ## Why
 *
 * 371 `<button>` elements carried **141 distinct className patterns**. That is
 * not a design system, it is 141 one-off decisions about padding, radius, and
 * which shade of grey the disabled state should be. It also means a change like
 * "make every primary button 1px shorter" is a 141-file search.
 *
 * Variants are a plain lookup rather than CVA. `class-variance-authority` is
 * not a dependency and the API surface here is small enough that the object
 * below does the same job in twenty lines.
 *
 * ## The cursor rule
 *
 * `index.css` sets `cursor: pointer` on every `button` in `@layer base`, so a
 * Button does not need to declare it. `disabled` gets `cursor: not-allowed`
 * from the same block.
 *
 * ## `as`
 *
 * A `<Link>` styled as a button is common (nav entries, "post a project").
 * Rendering a real `<button>` there would break middle-click, ctrl-click and
 * "open in new tab". `as={Link}` keeps the element correct and the styling
 * shared.
 */



/**
 * `Spinner` is imported lazily-ish (static import is fine, it is tiny) and shown
 * in place of the label so the button does not change width mid-submit. The
 * spinner is `aria-hidden` because the button's own `aria-busy` and the live
 * region in the form already announce the state.
 */
import { Loader2 } from "lucide-react";
import { BASE, VARIANTS, SIZES, ICON_BOXES } from "./buttonStyles";

export default function Button({
  as: Component = "button",
  variant = "primary",
  size = "md",
  loading = false,
  disabled = false,
  className = "",
  children,
  type,
  ...rest
}) {
  const isNativeButton = Component === "button";
  return (
    <Component
      // A <button> inside a <form> defaults to type="submit". Forgetting that
      // is how a "Cancel" button ends up submitting the form, so it is defaulted
      // explicitly and can be overridden.
      type={isNativeButton ? (type ?? "button") : undefined}
      disabled={isNativeButton ? disabled || loading : undefined}
      aria-busy={loading || undefined}
      aria-disabled={!isNativeButton && (disabled || loading) ? true : undefined}
      className={`${BASE} ${VARIANTS[variant] ?? VARIANTS.primary} ${SIZES[size] ?? SIZES.md} ${className}`}
      {...rest}
    >
      {loading ? <Loader2 className="w-4 h-4 animate-spin" aria-hidden="true" /> : null}
      {children}
    </Component>
  );
}

/**
 * Icon-only button. `label` is required and becomes `aria-label`, because an
 * icon carries no accessible name on its own -- these are the bell, the
 * messages envelope and the close X, none of which have visible text.
 */
export function IconButton({
  as: Component = "button",
  label,
  variant = "ghost",
  size = "md",
  loading = false,
  disabled = false,
  className = "",
  children,
  type,
  ...rest
}) {
  const isNativeButton = Component === "button";
  return (
    <Component
      type={isNativeButton ? (type ?? "button") : undefined}
      disabled={isNativeButton ? disabled || loading : undefined}
      aria-label={label}
      aria-busy={loading || undefined}
      className={`${BASE} ${VARIANTS[variant] ?? VARIANTS.ghost} ${ICON_BOXES[size] ?? ICON_BOXES.md} p-0 ${className}`}
      {...rest}
    >
      {loading ? <Loader2 className="w-4 h-4 animate-spin" aria-hidden="true" /> : children}
    </Component>
  );
}

