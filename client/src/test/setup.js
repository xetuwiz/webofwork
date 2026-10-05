/**
 * Vitest setup.
 *
 * Registers the jest-dom matchers, which add `toBeInTheDocument`,
 * `toBeDisabled`, `toHaveTextContent` and friends. They are assertions about the
 * rendered DOM rather than about JavaScript values, which is what most of these
 * tests are actually about -- and they produce a failure message that names the
 * element, where `expect(x).toBeTruthy()` says only "expected null to be truthy".
 *
 * `cleanup` is imported explicitly rather than relied upon as an automatic
 * global. Vitest only auto-cleans when `globals: true` *and* the testing-library
 * environment are both detected; stating it here means a forgotten global in the
 * config shows up as a duplicated-DOM failure naming this line, rather than as a
 * test that passes alone and fails in a file with two tests in it.
 */

import '@testing-library/jest-dom/vitest'
import { cleanup } from '@testing-library/react'
import { afterEach } from 'vitest'

afterEach(() => {
  cleanup()
})

/*
 * jsdom does not implement `matchMedia`, and several components read it to pick a
 * breakpoint. Left undefined, the first component to call it throws
 * "window.matchMedia is not a function" -- a DOM-platform gap reported as an
 * application bug, which is a bad afternoon.
 *
 * The values returned are the mobile-first defaults, so a test renders the
 * layout a phone gets unless it says otherwise. `addListener` and
 * `removeListener` are present because they are deprecated but still called by
 * library code, and their absence throws a different, equally confusing error.
 */
if (typeof window !== 'undefined' && !window.matchMedia) {
  window.matchMedia = (query) => ({
    matches: false,
    media: query,
    onchange: null,
    addListener: () => {},
    removeListener: () => {},
    addEventListener: () => {},
    removeEventListener: () => {},
    dispatchEvent: () => false,
  })
}

/*
 * jsdom has no layout engine, so `scrollIntoView` -- called by anything that
 * focuses an element out of view -- is simply absent. Same reasoning as
 * matchMedia: a platform gap, not an application fault.
 */
if (typeof Element !== 'undefined' && !Element.prototype.scrollIntoView) {
  Element.prototype.scrollIntoView = () => {}
}
