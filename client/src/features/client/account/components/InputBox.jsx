import { useId, useState } from "react";
import EyeShow from "../../../../assets/icons/eye-show-svgrepo-com.svg";
import EyeHide from "../../../../assets/icons/eye-password-hide-svgrepo-com.svg"

/**
 * Shared labelled text/password input.
 *
 * ## Why this was rewritten
 *
 * The label here was a bare `<label>{label}</label>` with no `htmlFor`, sitting
 * above an input with no `id`. It therefore named nothing, and this component
 * has **6 call sites** across SignInForm and SignupForm -- so it was 6 unlabelled
 * inputs, not one. Every field on both auth screens was announced as an
 * unlabelled "edit text" box.
 *
 * Fixing it here rather than at the call sites is the whole point of a shared
 * component: one `useId` closes all six.
 *
 * ## The password toggle
 *
 * The toggle button had no name of its own; it inherited one from the
 * `<img alt="show password">` inside it. That works, but it means the button's
 * accessible name is an asset's alt text, which nobody editing icons would
 * think to change. It is now an explicit `aria-label` plus `aria-pressed`, and
 * the image is marked decorative so the two cannot drift apart.
 */
export default function InputBox({
  label,
  name,
  type = "text",
  register,
  validation = {},
  error,
  placeholder = "",
  passwordToggle = false,
}) {
  const [show, setShow] = useState(false);
  const id = useId();
  const messageId = `${id}-message`;

  const inputType = passwordToggle ? (show ? "text" : "password") : type;

  return (
    <div className="mb-5">
      <label htmlFor={id} className="block font-poppins font-medium text-base text-fg-subtle mb-2">
        {label}
      </label>

      <div className="relative">
        <input
          id={id}
          type={inputType}
          placeholder={placeholder}
          {...register(name, validation)}
          aria-invalid={error ? true : undefined}
          // Only when there is a message to point at; a dangling
          // aria-describedby is worse than none.
          aria-describedby={error ? messageId : undefined}
          className="w-full h-[41px] px-4 pr-12 font-poppins text-base text-fg-subtle border border-line-strong rounded-md focus-visible:ring-2 focus-visible:ring-brand"
        />

        {passwordToggle && (
          <button
            type="button"
            onClick={() => setShow(!show)}
            aria-label={show ? "Hide password" : "Show password"}
            aria-pressed={show}
            aria-controls={id}
            className="absolute right-3 top-1/2 -translate-y-1/2"
          >
            {show ? (
              <img src={EyeHide} alt="" aria-hidden="true" className="w-5 h-5" />
            ) : (
              <img src={EyeShow} alt="" aria-hidden="true" className="w-5 h-5" />
            )}
          </button>
        )}
      </div>

      {error && (
        <p id={messageId} role="alert" className="text-danger-soft-fg text-xs mt-1">
          {error.message}
        </p>
      )}
    </div>
  );
}
