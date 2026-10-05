/**
 * Theme primitives, deliberately separate from the provider component.
 *
 * `ThemeProvider.jsx` exports both a component and these helpers, which trips
 * `react-refresh/only-export-components` and breaks Vite Fast Refresh for
 * anything importing it. The rule's own guidance is to put shared functions in
 * their own module, which is what this is.
 */

/** The `localStorage` key. Also inlined in the boot script in `index.html`. */
export const THEME_STORAGE_KEY = "wow-theme";

/**
 * Collapses a stored preference to the theme actually in effect.
 *
 * @param {"light" | "dark" | "system"} theme
 * @returns {"light" | "dark"}
 */
export function resolveTheme(theme) {
  if (theme === "light" || theme === "dark") return theme;
  if (typeof window === "undefined") return "light";
  return window.matchMedia?.("(prefers-color-scheme: dark)").matches ? "dark" : "light";
}

/**
 * The class-based dark mode requires `.dark` on <html>.
 *
 * @param {"light" | "dark"} resolved
 */
export function applyTheme(resolved) {
  if (typeof document === "undefined") return;
  const root = document.documentElement;
  root.classList.toggle("dark", resolved === "dark");
  // Keeps native UI (scrollbars, form controls, caret) in step. Without this a
  // dark page still renders light scrollbars and light date-picker chrome.
  root.style.colorScheme = resolved;
}

/**
 * Reads the stored preference, falling back to `system` for anything outside the
 * enum -- a hand-edited or stale value must not render an undefined theme.
 *
 * @returns {"light" | "dark" | "system"}
 */
export function readStoredTheme() {
  if (typeof window === "undefined") return "system";
  try {
    const stored = window.localStorage.getItem(THEME_STORAGE_KEY);
    if (stored === "light" || stored === "dark" || stored === "system") return stored;
  } catch {
    // Blocked storage (private mode). `system` still works, it just will not
    // survive a reload.
  }
  return "system";
}
