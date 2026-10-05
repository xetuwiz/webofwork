/**
 * Contextual empty state.
 *
 * ## Why three variants
 *
 * The app had ~10 empty-state strings, all the same shape: "No reviews found",
 * "No skills listed yet.", "No results found". But an empty list means three
 * genuinely different things, and the message decides what the user does next:
 *
 *  * `first-use` -- the account is new and there is nothing *because* the user
 *    has not done anything yet. The fix is an action, not a search.
 *  * `no-match` -- a search or filter is hiding real data. The fix is to widen
 *    or clear the filter. Saying "no reviews found" here is actively wrong: it
 *    tells someone their client has no reviews when in fact the filter is
 *    too narrow.
 *  * `error` -- the request failed. Showing an empty state implies the answer
 *    was "nothing", which is a lie; the data is unknown.
 *  * `none` -- genuinely nothing, ever. Rare; mostly for system-level lists.
 *
 * The previous copy made 2 and 3 indistinguishable from 1, and made 3
 * indistinguishable from 2.
 *
 * ## Defaults
 *
 * Each variant ships a sensible default title/description so a call site is
 * usually just `<EmptyState variant="no-match" onClear={...} />`, and every
 * variant is overridable for the cases where the copy is genuinely specific.
 */
import { SearchX, Inbox, AlertTriangle, RotateCcw, Plus } from "lucide-react";
import Button from "./Button";

const VARIANTS = {
  "first-use": {
    Icon: Plus,
    title: "Nothing here yet",
    description: "Once you add something, it will show up here.",
    // An invitation to act, not a report of absence.
    action: "primary",
  },
  "no-match": {
    Icon: SearchX,
    title: "No results found",
    description: "Nothing matched your search. Try different keywords or clear the filters.",
    // The action is to widen, not to create -- creating would be the wrong
    // response to a search that came back empty.
    action: "clear",
  },
  none: {
    Icon: Inbox,
    title: "Nothing here",
    description: "There is nothing to show.",
    action: null,
  },
  error: {
    Icon: AlertTriangle,
    title: "Could not load this",
    description: "Something went wrong fetching the data. This is a problem on our side, not an empty result.",
    action: "retry",
  },
};

export default function EmptyState({
  variant = "none",
  title,
  description,
  actionLabel,
  onAction,
  onClearFilters,
  className = "",
  children,
}) {
  const config = VARIANTS[variant] ?? VARIANTS.none;
  const { Icon } = config;
  const heading = title ?? config.title;
  const body = description ?? config.description;
  const tone =
    variant === "error"
      ? "text-danger"
      : variant === "no-match"
        ? "text-fg-subtle"
        : "text-brand";

  return (
    <div
      className={`flex flex-col items-center justify-center text-center py-14 px-6 ${className}`}
    >
      <div
        className={`w-14 h-14 rounded-2xl flex items-center justify-center mb-4 ${
          variant === "error" ? "bg-danger-soft" : "bg-surface-3"
        }`}
      >
        <Icon className={`w-6 h-6 ${tone}`} aria-hidden="true" />
      </div>

      <h3 className="text-base font-bold text-fg mb-1">{heading}</h3>
      <p className="text-sm text-fg-subtle max-w-sm leading-relaxed">{body}</p>

      {children}

      {actionLabel && onAction ? (
        <Button
          variant={config.action === "clear" ? "outline" : "primary"}
          size="sm"
          onClick={onAction}
          className="mt-5"
        >
          {actionLabel}
        </Button>
      ) : null}

      {/* A no-match state is nearly always fixable by clearing the filter, so
          that gets offered without the caller having to think about it. */}
      {variant === "no-match" && onClearFilters ? (
        <Button variant="ghost" size="sm" onClick={onClearFilters} className="mt-2">
          <RotateCcw className="w-3.5 h-3.5" aria-hidden="true" />
          Clear filters
        </Button>
      ) : null}
    </div>
  );
}
