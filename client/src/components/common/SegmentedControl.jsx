/**
 * Segmented control -- a two-or-more option switch with a sliding active pill.
 *
 * ## Why this exists
 *
 * Three places in the app had the same hand-rolled tab strip: the signature
 * Type/Draw switch in ContractOfferModal and OfferModal, and the Direct
 * Upload / External Link switch in the freelancer ContractDetail. All three
 * painted the active background onto the button itself with
 * `bg-surface ... shadow-sm`, so the highlight teleported between options
 * instead of moving.
 *
 * A shared `layoutId` is what makes it slide: framer-motion animates the one
 * element between its two positions, so there is only ever a single pill in the
 * DOM rather than one per option fading in and out.
 *
 * ## Why a spring, not the duration tokens
 *
 * The rest of the app uses the `--duration-*` tokens. A segmented control is the
 * one place a duration is the wrong tool: these get clicked in bursts, and a
 * fixed-duration tween restarted mid-flight jumps to the new target rather than
 * continuing from where it was. A spring carries its velocity through, so
 * hammering the toggle reads as one continuous motion. Reduced motion is
 * already handled globally by `<MotionConfig reducedMotion="user">` in
 * main.jsx, which suppresses the layout animation entirely.
 */
// Aliased to a capitalised name on purpose. This project has no
// `react/jsx-uses-vars` rule; it relies on `no-unused-vars` with
// `varsIgnorePattern: '^[A-Z_]'` to excuse components that are only referenced
// in JSX. The lowercase `motion` export does not match that pattern and was
// reported as an unused import even though `<Motion.span>` is four lines below.
import { motion as Motion } from "framer-motion";

/** Shared so all three instances feel identical. */
const PILL_SPRING = { type: "spring", stiffness: 420, damping: 34 };

const SIZES = {
  sm: "px-3 py-1 text-xs font-medium",
  md: "px-3 py-1.5 text-xs font-medium",
  lg: "flex-1 py-2 text-[10px] font-black uppercase tracking-widest",
};

export default function SegmentedControl({
  options,
  value,
  onChange,
  layoutId,
  size = "md",
  className = "",
}) {
  return (
    <div
      role="tablist"
      className={`flex gap-1 bg-surface-3 rounded-lg p-1 ${size === "lg" ? "p-1.5" : "p-1"} ${className}`}
    >
      {options.map((option) => {
        const active = option.value === value;
        const Icon = option.icon;
        return (
          <button
            key={option.value}
            type="button"
            role="tab"
            aria-selected={active}
            // Space/Enter already activate a <button>; this gives the arrow-key
            // behaviour a tablist is expected to have.
            onKeyDown={(event) => {
              if (event.key !== "ArrowRight" && event.key !== "ArrowLeft") return;
              event.preventDefault();
              const i = options.findIndex((o) => o.value === value);
              const next =
                event.key === "ArrowRight"
                  ? (i + 1) % options.length
                  : (i - 1 + options.length) % options.length;
              onChange(options[next].value);
            }}
            onClick={() => onChange(option.value)}
            className={`relative rounded-md ${SIZES[size]} ${
              active ? "text-brand" : "text-fg-muted"
            }`}
          >
            {active ? (
              <Motion.span
                layoutId={layoutId}
                className="absolute inset-0 rounded-md bg-surface shadow-sm"
                transition={PILL_SPRING}
                aria-hidden="true"
              />
            ) : null}
            <span className="relative inline-flex items-center justify-center gap-1.5">
              {Icon ? <Icon className="w-3.5 h-3.5" aria-hidden="true" /> : null}
              {option.label}
            </span>
          </button>
        );
      })}
    </div>
  );
}
