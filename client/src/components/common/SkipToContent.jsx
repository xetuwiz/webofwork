/**
 * "Skip to main content" link.
 *
 * WCAG 2.4.1 Bypass Blocks (Level A). The app renders a persistent header and,
 * on every authenticated page, a persistent sidebar with 5-8 links. Without
 * this, a keyboard or screen-reader user tabs through that whole block on every
 * single page transition -- 8-10 wasted tab stops to reach content that is
 * immediately after it.
 *
 * The link is visually hidden until focused (`sr-only` -> `not-sr-only`), which
 * is the standard pattern: it must be in the tab order and reachable, but it
 * should not be a permanent piece of chrome for sighted mouse users.
 *
 * ## Why the target needs `tabIndex={-1}`
 *
 * Moving focus to an element only works if that element is focusable. `<main>`
 * is not focusable by default, so the `href="#main-content"` jump scrolls but
 * leaves focus on the skip link -- meaning the next Tab press returns to the top
 * of the header and the user has to do it all again. The layouts and the
 * standalone pages therefore carry `tabIndex={-1}` on the target.
 *
 * ## Why it is rendered in RootLayout
 *
 * One instance, mounted above the router, so it is present on every route
 * including the public pages. The target is unique at any moment because the
 * nested `<main>` elements inside the role layouts were demoted to `<div>` --
 * two `<main>` landmarks on one page is itself an accessibility fault, since a
 * screen reader announces "main" twice and the link would have had two
 * candidates.
 */
export default function SkipToContent() {
  return (
    <a
      href="#main-content"
      className="sr-only focus:not-sr-only focus:fixed focus:top-4 focus:left-4 focus:z-toast focus:inline-flex focus:items-center focus:gap-2 focus:rounded-lg focus:bg-brand focus:px-4 focus:py-2.5 focus:text-sm focus:font-semibold focus:text-brand-fg focus:shadow-lg focus:outline-none"
    >
      Skip to main content
    </a>
  );
}
