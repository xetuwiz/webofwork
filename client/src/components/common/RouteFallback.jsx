/**
 * Fallback shown while a lazy route chunk downloads.
 *
 * Deliberately a content skeleton rather than a spinner or a blank page: the
 * route tree renders inside the app shell, so a spinner here would replace the
 * nav and header with a centred circle on every navigation.
 *
 * `aria-busy` rather than `role="status"`: the skeleton is decorative (all of it
 * is `aria-hidden`), and announcing "Loading" on every navigation is noise for a
 * screen-reader user who already knows they pressed a link. The route's own
 * `h1` announces once the page lands.
 */
export default function RouteFallback() {
  return (
    <div
      aria-busy="true"
      className="mx-auto w-full max-w-7xl px-4 py-8 sm:px-6 lg:px-8"
    >
      <div className="space-y-6" aria-hidden="true">
        <div className="h-8 w-56 rounded-lg shimmer bg-surface-3" />
        <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-3">
          {Array.from({ length: 6 }).map((_, i) => (
            <div
              key={i}
              className="rounded-xl border border-line bg-surface p-5"
            >
              <div className="mb-3 h-4 w-3/4 rounded shimmer bg-surface-3" />
              <div className="mb-2 h-3 w-full rounded shimmer bg-surface-3" />
              <div className="h-3 w-2/3 rounded shimmer bg-surface-3" />
            </div>
          ))}
        </div>
      </div>
    </div>
  );
}
