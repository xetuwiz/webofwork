import { createContext, useContext, useEffect, useMemo, useState, useCallback } from "react";
import { applyTheme, readStoredTheme, resolveTheme } from "./theme";

/**
 * Theme: `light`, `dark`, or `system`.
 *
 * `system` is kept as a distinct stored value rather than being collapsed into
 * whatever the OS currently says. Collapsing it means a user who never chose a
 * theme can never be told "you're following your system" -- and, worse, the
 * moment they pick a theme we can never restore the previous OS-driven state.
 */
const ThemeContext = createContext(null);

export function ThemeProvider({ children }) {
  const [theme, setThemeState] = useState(readStoredTheme);

  useEffect(() => {
    applyTheme(resolveTheme(theme));
  }, [theme]);

  // Only follow the OS while the user is actually on "system". Leaving this
  // subscribed for an explicit choice would silently overwrite their pick the
  // next time the OS switched at sunset.
  useEffect(() => {
    if (theme !== "system") return;
    const media = window.matchMedia?.("(prefers-color-scheme: dark)");
    if (!media) return;
    const onChange = () => applyTheme(resolveTheme("system"));
    media.addEventListener("change", onChange);
    return () => media.removeEventListener("change", onChange);
  }, [theme]);

  const setTheme = useCallback((next) => {
    setThemeState(next);
    try {
      window.localStorage.setItem("wow-theme", next);
    } catch {
      // Non-fatal: the theme still applies for this session.
    }
  }, []);

  // Cycles light -> dark -> light, which is what people expect from a single
  // toggle. It deliberately does not cycle through "system": reaching system
  // again after darkening is not something anyone is looking for, and the
  // segmented control is where "system" belongs.
  const toggle = useCallback(() => {
    setTheme(resolveTheme(theme) === "dark" ? "light" : "dark");
  }, [theme, setTheme]);

  const value = useMemo(
    () => ({
      theme,
      resolvedTheme: resolveTheme(theme),
      setTheme,
      toggle,
    }),
    [theme, setTheme, toggle]
  );

  return <ThemeContext.Provider value={value}>{children}</ThemeContext.Provider>;
}

export function useTheme() {
  const ctx = useContext(ThemeContext);
  // A hard throw beats `undefined` access: a missing provider is a wiring bug in
  // main.jsx, and it should fail at the point of the mistake rather than deep
  // inside a leaf component with "cannot read properties of null".
  if (!ctx) {
    throw new Error("useTheme must be used inside <ThemeProvider>");
  }
  return ctx;
}
