/**
 * Form field primitives: `Field` (label + control + message), `Input`,
 * `Textarea`, `Select`.
 *
 * ## Why
 *
 * 33 distinct `<input>` class patterns, and the label/error association was
 * hand-rolled each time. The most common version was a bare `<label>` above an
 * `<input>` with no `htmlFor`, which means a screen reader announces the field
 * as "edit text" with no name at all.
 *
 * `Field` makes that impossible to get wrong: the id is generated once, wired
 * to the label, the control and the message, and `aria-describedby` is only
 * attached when there actually is a message -- pointing it at a missing id is
 * worse than omitting it.
 *
 * The error is a live region (`role="alert"`) so a validation message that
 * appears after submit is announced, rather than silently changing a red border
 * that a screen-reader user is never told about.
 */
import { useId } from "react";
import { AlertCircle } from "lucide-react";
import { CONTROL } from "./formStyles";


/**
 * Wraps a control with its label and optional message.
 *
 * `children` is a render function receiving the props that must be spread onto
 * the control (`id`, `aria-invalid`, `aria-describedby`). A function rather
 * than cloning, so this does not depend on the child being a DOM element.
 */
export function Field({ label, error, hint, required, className = "", children, id: idProp }) {
  const generated = useId();
  const id = idProp ?? generated;
  const messageId = `${id}-message`;

  return (
    <div className={`space-y-2 ${className}`}>
      {label ? (
        <label
          htmlFor={id}
          className="block text-[10px] font-bold text-fg-subtle uppercase tracking-widest"
        >
          {label}
          {required ? <span className="text-danger ml-1">*</span> : null}
        </label>
      ) : null}

      {typeof children === "function"
        ? children({
            id,
            "aria-invalid": error ? true : undefined,
            "aria-describedby": error || hint ? messageId : undefined,
          })
        : children}

      {error || hint ? (
        <p
          id={messageId}
          role={error ? "alert" : undefined}
          className={`flex items-center gap-1.5 text-xs font-medium ${
            error ? "text-danger" : "text-fg-subtle"
          }`}
        >
          {error ? <AlertCircle className="w-3.5 h-3.5 shrink-0" aria-hidden="true" /> : null}
          {error || hint}
        </p>
      ) : null}
    </div>
  );
}

export function Input({ error, className = "", icon: Icon, ...rest }) {
  const control = (
    <input
      aria-invalid={error ? true : undefined}
      className={`${CONTROL} h-11 ${error ? "border-danger" : "border-line"} ${
        Icon ? "pl-9" : ""
      } ${className}`}
      {...rest}
    />
  );

  if (!Icon) return control;

  return (
    <div className="relative">
      <Icon
        className="w-4 h-4 absolute left-3 top-1/2 -translate-y-1/2 text-fg-subtle pointer-events-none"
        aria-hidden="true"
      />
      {control}
    </div>
  );
}

export function Textarea({ error, className = "", rows = 4, ...rest }) {
  return (
    <textarea
      rows={rows}
      aria-invalid={error ? true : undefined}
      className={`${CONTROL} py-2.5 resize-y ${error ? "border-danger" : "border-line"} ${className}`}
      {...rest}
    />
  );
}

export function Select({ error, className = "", children, ...rest }) {
  return (
    <div className="relative">
      <select
        aria-invalid={error ? true : undefined}
        className={`${CONTROL} h-11 appearance-none pr-9 ${
          error ? "border-danger" : "border-line"
        } ${className}`}
        {...rest}
      >
        {children}
      </select>
      {/* Native selects draw their own arrow; this replaces it with one that
          follows the theme, and is hidden from assistive tech because the
          control's own semantics already convey "choose one". */}
      <svg
        className="w-4 h-4 absolute right-3 top-1/2 -translate-y-1/2 text-fg-subtle pointer-events-none"
        viewBox="0 0 20 20"
        fill="none"
        aria-hidden="true"
      >
        <path
          d="m6 8 4 4 4-4"
          stroke="currentColor"
          strokeWidth="1.5"
          strokeLinecap="round"
          strokeLinejoin="round"
        />
      </svg>
    </div>
  );
}

/** Bare control classes, for the odd input that is not wrapped in a Field. */
