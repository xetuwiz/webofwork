import { useTheme } from "./ThemeProvider";

/**
 * The three-way control: a compact icon button for the sidebar/header, and a
 * segmented row when there is room to show all three options.
 *
 * The icon button cycles light -> dark -> light, which is what people expect from
 * a single toggle. It deliberately does not cycle through "system": reaching
 * system again after darkening is not something anyone is looking for, and the
 * segmented variant is where "system" belongs.
 */
export function ThemeToggle({ className = "" }) {
  const { resolvedTheme, toggle } = useTheme();

  const isDark = resolvedTheme === "dark";

  return (
    <button
      type="button"
      onClick={toggle}
      // The accessible name already says what the control does. There used to
      // be a `title` alongside it saying "Light theme" / "Dark theme", which was
      // both redundant and contradictory -- two different strings competing to
      // name the same button, and the hover tooltip gave a sighted user less
      // information than the label a screen reader got. `title` is also
      // hover-only, so it cannot be the primary name for a control that is
      // reachable by keyboard.
      aria-label={isDark ? "Switch to light theme" : "Switch to dark theme"}
      className={`inline-flex h-9 w-9 items-center justify-center rounded-lg border border-line bg-surface text-fg-muted transition-colors hover:bg-surface-3 hover:text-fg outline-none focus-visible:ring-2 focus-visible:ring-brand ${className}`}
    >
      {/* Both icons are rendered and swapped with `hidden`, so switching themes
          does not remount the SVG and lose the rotation animation entry state. */}
      <SunIcon className={isDark ? "hidden" : "block"} />
      <MoonIcon className={isDark ? "block" : "hidden"} />
    </button>
  );
}

/** Explicit three-way selector. */
export function ThemeSegmentedControl({ className = "" }) {
  const { theme, setTheme } = useTheme();

  // `icon` lowercase then aliased to `Icon` below, rather than destructuring a
  // capitalised `Icon` out of the callback: this eslint config's
  // no-unused-vars does not count a JSX element as a use for a destructured
  // callback parameter, so `{ Icon }` in the map signature is reported as unused.
  // This is also the pattern the sidebars already use.
  const options = [
    { value: "light", label: "Light", icon: SunIcon },
    { value: "dark", label: "Dark", icon: MoonIcon },
    { value: "system", label: "System", icon: SystemIcon },
  ];

  return (
    <div
      role="radiogroup"
      aria-label="Colour theme"
      className={`inline-flex items-center gap-1 rounded-lg border border-line bg-surface-2 p-1 ${className}`}
    >
      {options.map(({ value, label, icon }) => {
        const Icon = icon;
        const active = theme === value;
        return (
          <button
            key={value}
            type="button"
            role="radio"
            aria-checked={active}
            onClick={() => setTheme(value)}
            className={`inline-flex items-center gap-1.5 rounded-md px-2.5 py-1.5 text-xs font-semibold transition-colors outline-none focus-visible:ring-2 focus-visible:ring-brand ${
              active
                ? "bg-surface text-fg shadow-sm"
                : "text-fg-muted hover:text-fg"
            }`}
          >
            <Icon className="h-3.5 w-3.5" />
            {label}
          </button>
        );
      })}
    </div>
  );
}

function SunIcon({ className = "" }) {
  return (
    <svg
      className={`h-[18px] w-[18px] ${className}`}
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      strokeWidth="1.75"
      strokeLinecap="round"
      aria-hidden="true"
    >
      <circle cx="12" cy="12" r="4" />
      <path d="M12 2v2M12 20v2M4.93 4.93l1.41 1.41M17.66 17.66l1.41 1.41M2 12h2M20 12h2M4.93 19.07l1.41-1.41M17.66 6.34l1.41-1.41" />
    </svg>
  );
}

function MoonIcon({ className = "" }) {
  return (
    <svg
      className={`h-[18px] w-[18px] ${className}`}
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      strokeWidth="1.75"
      strokeLinecap="round"
      strokeLinejoin="round"
      aria-hidden="true"
    >
      <path d="M21 12.79A9 9 0 1 1 11.21 3 7 7 0 0 0 21 12.79z" />
    </svg>
  );
}

function SystemIcon({ className = "" }) {
  return (
    <svg
      className={`h-[18px] w-[18px] ${className}`}
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      strokeWidth="1.75"
      strokeLinecap="round"
      strokeLinejoin="round"
      aria-hidden="true"
    >
      <rect x="2" y="4" width="20" height="13" rx="2" />
      <path d="M8 21h8M12 17v4" />
    </svg>
  );
}

export default ThemeToggle;
