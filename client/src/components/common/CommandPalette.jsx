/**
 * Command palette -- jump anywhere with Cmd/Ctrl + K.
 *
 * ## Why
 *
 * The app has ~30 destinations spread across three sidebars, a header nav, a
 * profile sidebar and a handful of icon-only buttons. Reaching a specific page
 * currently means knowing which surface it lives on. A palette turns that into
 * one text field, and it is the single fastest fix for navigation depth in an
 * app this size.
 *
 * ## Keyboard behaviour
 *
 * Arrow keys move the highlight, Enter navigates, Escape closes. The list is
 * driven with `aria-activedescendant` rather than moving DOM focus, because
 * moving focus into a list on every keystroke makes screen readers announce
 * the whole list again and loses the input's focus. Focus stays in the input.
 *
 * ## Where it is mounted
 *
 * `RootLayout`, above the router, so the shortcut works on every route. It reads
 * the role from the store and filters through `filterByRole`, so a freelancer
 * never sees the admin console and a signed-out visitor sees only public pages.
 */
import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { useNavigate } from "react-router-dom";
import { useSelector } from "react-redux";
import { Search, CornerDownLeft, ArrowUp, ArrowDown } from "lucide-react";
import { filterByRole, searchNavigation } from "../../config/navigation";

/** Matches Cmd+K on macOS, Ctrl+K everywhere else. */
function isPaletteShortcut(event) {
  return (event.metaKey || event.ctrlKey) && event.key.toLowerCase() === "k";
}

export default function CommandPalette({ open, onClose }) {
  const navigate = useNavigate();
  const user = useSelector((state) => state.auth.user);
  const [query, setQuery] = useState("");
  const [rawActiveIndex, setActiveIndex] = useState(0);
  const inputRef = useRef(null);
  const listRef = useRef(null);

  const results = useMemo(() => {
    const allowed = filterByRole(user?.current_role);
    return searchNavigation(query, allowed);
  }, [query, user?.current_role]);

  // Focus on open. Deliberately does NOT reset `query` here: clearing state in
  // an effect is a set-state-in-effect, and it also produces a frame where the
  // list renders with the previous search before the reset lands. Instead the
  // host passes a changing `key`, so opening remounts this component with clean
  // state and the reset is simply the initial value.
  useEffect(() => {
    if (!open) return undefined;
    // The dialog is rendered by the caller, so the input may not exist on the
    // same tick as the state flip.
    const id = requestAnimationFrame(() => inputRef.current?.focus());
    return () => cancelAnimationFrame(id);
  }, [open]);

  // Derived, not synchronised. Typing narrows `results` and can leave the
  // stored index past the end of the list; clamping at read time means there is
  // no effect writing state after render, and therefore no frame where the
  // highlight points at a row that no longer exists.
  const activeIndex =
    results.length === 0 ? 0 : Math.min(rawActiveIndex, results.length - 1);

  // Keep the highlighted row in view while arrowing through a long list.
  useEffect(() => {
    if (!open) return;
    const node = listRef.current?.querySelector('[data-active="true"]');
    node?.scrollIntoView({ block: "nearest" });
  }, [activeIndex, open]);

  const go = useCallback(
    (item) => {
      if (!item) return;
      onClose();
      navigate(item.path);
    },
    [navigate, onClose],
  );

  const onKeyDown = useCallback(
    (event) => {
      switch (event.key) {
        case "Escape":
          event.preventDefault();
          onClose();
          break;
        case "ArrowDown":
          event.preventDefault();
          setActiveIndex((i) => (results.length ? (i + 1) % results.length : 0));
          break;
        case "ArrowUp":
          event.preventDefault();
          setActiveIndex((i) => (results.length ? (i - 1 + results.length) % results.length : 0));
          break;
        case "Enter":
          event.preventDefault();
          go(results[activeIndex]);
          break;
        default:
          break;
      }
    },
    [activeIndex, go, onClose, results],
  );

  if (!open) return null;

  return (
    // The scrim closes on click but is deliberately not a <button>: it is a
    // click-away affordance, and the keyboard path is Escape, which is handled
    // above. Giving it a role would put a nameless control in the tab order.
    <div
      className="fixed inset-0 z-modal bg-inverse/60 backdrop-blur-sm flex items-start justify-center p-4 pt-[12vh]"
      onClick={onClose}
      data-testid="command-palette-scrim"
    >
      <div
        role="dialog"
        aria-modal="true"
        aria-label="Command palette"
        className="w-full max-w-dialog-lg bg-surface border border-line-soft rounded-2xl shadow-2xl overflow-hidden animate-in fade-in zoom-in-95 duration-fast"
        onClick={(e) => e.stopPropagation()}
        onKeyDown={onKeyDown}
      >
        <div className="flex items-center gap-3 px-5 border-b border-line-soft">
          <Search className="w-5 h-5 text-fg-subtle shrink-0" aria-hidden="true" />
          <input
            ref={inputRef}
            type="text"
            value={query}
            onChange={(e) => {
              setQuery(e.target.value);
              setActiveIndex(0);
            }}
            placeholder="Jump to a page..."
            aria-label="Search pages"
            aria-controls="command-palette-results"
            aria-activedescendant={
              results.length ? `command-palette-item-${activeIndex}` : undefined
            }
            autoComplete="off"
            className="flex-1 bg-transparent py-4 text-base text-fg placeholder:text-fg-subtle outline-none"
          />
          <kbd className="hidden sm:inline-flex items-center gap-1 text-[10px] font-bold text-fg-subtle border border-line rounded px-1.5 py-0.5">
            ESC
          </kbd>
        </div>

        <ul
          ref={listRef}
          id="command-palette-results"
          role="listbox"
          aria-label="Results"
          className="max-h-[52vh] overflow-y-auto p-2"
        >
          {results.length === 0 ? (
            <li className="px-4 py-10 text-center text-sm text-fg-subtle">
              No pages match “{query.trim()}”
            </li>
          ) : (
            results.map((item, index) => {
              const Icon = item.icon;
              const active = index === activeIndex;
              return (
                <li key={`${item.group}-${item.path}`} role="none">
                  <button
                    type="button"
                    id={`command-palette-item-${index}`}
                    role="option"
                    aria-selected={active}
                    data-active={active}
                    onMouseEnter={() => setActiveIndex(index)}
                    onClick={() => go(item)}
                    className={`w-full flex items-center gap-3 px-3 py-2.5 rounded-xl text-left transition-colors ${
                      active ? "bg-brand-soft text-brand-soft-fg" : "text-fg hover:bg-surface-3"
                    }`}
                  >
                    <Icon className="w-4 h-4 shrink-0" aria-hidden="true" />
                    <span className="flex-1 text-sm font-semibold">{item.label}</span>
                    <span className="text-[10px] font-bold uppercase tracking-widest text-fg-subtle">
                      {item.group}
                    </span>
                  </button>
                </li>
              );
            })
          )}
        </ul>

        <div className="flex items-center gap-4 px-5 py-3 border-t border-line-soft text-[10px] font-bold uppercase tracking-widest text-fg-subtle">
          <span className="inline-flex items-center gap-1.5">
            <ArrowUp className="w-3 h-3" aria-hidden="true" />
            <ArrowDown className="w-3 h-3" aria-hidden="true" />
            navigate
          </span>
          <span className="inline-flex items-center gap-1.5">
            <CornerDownLeft className="w-3 h-3" aria-hidden="true" />
            open
          </span>
          <span className="ml-auto">{results.length} result{results.length === 1 ? "" : "s"}</span>
        </div>
      </div>
    </div>
  );
}

/**
 * Mounts the global Cmd/Ctrl+K listener and renders the palette.
 *
 * Kept separate from the presentational component above so the `open` state and
 * the key listener live in one place, and `RootLayout` only has to render
 * `<CommandPaletteHost />`.
 */
export function CommandPaletteHost() {
  const [open, setOpen] = useState(false);

  useEffect(() => {
    const onKeyDown = (event) => {
      if (isPaletteShortcut(event)) {
        event.preventDefault();
        setOpen((v) => !v);
        return;
      }
      // Plain "/" is the other convention, but only when the user is not already
      // typing -- otherwise it would insert a slash into every search box.
      if (
        event.key === "/" &&
        !event.metaKey &&
        !event.ctrlKey &&
        !event.altKey &&
        !(event.target instanceof HTMLElement &&
          (event.target.tagName === "INPUT" ||
            event.target.tagName === "TEXTAREA" ||
            event.target.isContentEditable))
      ) {
        event.preventDefault();
        setOpen(true);
      }
    };
    window.addEventListener("keydown", onKeyDown);
    return () => window.removeEventListener("keydown", onKeyDown);
  }, []);

  // The key changes on every open, so the palette remounts with an empty query
  // and a reset highlight. See the note in CommandPalette about why that is not
  // done with an effect.
  return <CommandPalette key={open ? 'open' : 'closed'} open={open} onClose={() => setOpen(false)} />;
}
