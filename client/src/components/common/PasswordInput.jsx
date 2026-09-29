/**
 * Password input with a reveal toggle.
 *
 * ## Why a primitive
 *
 * Three files rolled this by hand, and they had already drifted apart:
 *
 *  * `AdminLogin.jsx` — one toggle, `top-3.5`, 16px icons
 *  * `AccountSettings.jsx` — three toggles in a `{ current, new, confirm }` map
 *  * `ResetPasswordPage.jsx` — **no toggle at all**, two bare `type="password"`
 *    fields, so a user setting a new password could not check what they typed
 *
 * Same control, three different affordances. One implementation makes the third
 * case impossible to forget.
 *
 * ## The toggle is a real button
 *
 * It declares `type="button"`, because it usually sits inside a `<form>` and a
 * bare `<button>` defaults to `type="submit"` -- so clicking "show password"
 * would submit the form. It also carries `aria-pressed` and an `aria-label` that
 * changes with state, so a screen reader announces "Show password, button,
 * not pressed" and then "Hide password, button, pressed".
 *
 * The field itself is switched between `password` and `text` rather than being
 * overlaid, so browser password managers and the password input's own semantics
 * keep working.
 */
import { useId, useState } from "react";
import { Eye, EyeOff } from "lucide-react";

/**
 * Optional strength guidance.
 *
 * Deliberately *actionable* rather than a vanity bar: it names the concrete
 * thing to change ("add a number or symbol") instead of scoring the password
 * "medium". A meter that says "medium" on a password the API will reject has
 * taught the user nothing.
 */
function scorePassword(value) {
  if (!value) return { score: 0, advice: null };
  const checks = [
    { ok: value.length >= 8, advice: "Use at least 8 characters" },
    { ok: /[a-z]/.test(value) && /[A-Z]/.test(value), advice: "Mix upper and lower case" },
    { ok: /\d/.test(value), advice: "Add a number" },
    { ok: /[^A-Za-z0-9]/.test(value), advice: "Add a symbol" },
  ];
  const score = checks.filter((c) => c.ok).length;
  const next = checks.find((c) => !c.ok);
  return { score, advice: next ? next.advice : null };
}

const TONE = [
  "bg-surface-3",
  "bg-danger",
  "bg-warning",
  "bg-brand",
  "bg-success",
];

const TONE_TEXT = [
  "text-fg-subtle",
  "text-danger",
  "text-warning",
  "text-brand",
  "text-success",
];

const LABEL = ["", "Weak", "Fair", "Good", "Strong"];

export default function PasswordInput({
  showStrength = false,
  className = "",
  ...rest
}) {
  const [visible, setVisible] = useState(false);
  const strengthId = useId();

  const { score, advice } = scorePassword(rest.value ?? "");

  return (
    <div>
      <div className="relative">
        <input
          type={visible ? "text" : "password"}
          // Reserve room for the button so the value never runs under it.
          className={`pr-11 ${className}`}
          aria-describedby={showStrength && strengthId ? strengthId : undefined}
          {...rest}
        />
        <button
          type="button"
          onClick={() => setVisible((v) => !v)}
          aria-label={visible ? "Hide password" : "Show password"}
          aria-pressed={visible}
          className="absolute right-3 top-1/2 -translate-y-1/2 p-1 text-fg-subtle hover:text-fg-muted transition-colors"
        >
          {visible ? <EyeOff size={16} aria-hidden="true" /> : <Eye size={16} aria-hidden="true" />}
        </button>
      </div>

      {showStrength ? (
        <div id={strengthId} className="mt-2">
          <div className="flex gap-1" aria-hidden="true">
            {[1, 2, 3, 4].map((n) => (
              <div
                key={n}
                className={`h-1 flex-1 rounded-full transition-colors ${
                  n <= score ? TONE[score] : "bg-surface-3"
                }`}
              />
            ))}
          </div>
          <p className={`mt-1.5 text-xs font-medium ${TONE_TEXT[score]}`}>
            {rest.value
              ? `${LABEL[score]}${advice ? ` — ${advice.toLowerCase()}` : ""}`
              : "Use at least 8 characters"}
          </p>
        </div>
      ) : null}
    </div>
  );
}
