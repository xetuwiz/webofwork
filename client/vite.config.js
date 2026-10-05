import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'
import tailwindcss from '@tailwindcss/vite'

export default defineConfig({
  /*
   * Tests.
   *
   * In the same file as the build config on purpose: `test` needs to know how
   * the app is transformed (the React plugin, so JSX compiles) and a separate
   * `vitest.config.js` would have to duplicate that, and the two would drift the
   * first time a plugin was added here.
   *
   * `environment: 'jsdom'` because these are component tests. `globals: true` so
   * a spec does not have to import `describe`/`it`/`expect` -- the assertions are
   * the point of a test, and the boilerplate is not.
   *
   * `setupFiles` loads the jest-dom matchers, which is what makes
   * `toBeInTheDocument()` and `toBeDisabled()` available. Without it those
   * matchers are undefined and the failure reads as "expect is not a function",
   * which sends you looking in the wrong file.
   *
   * The include pattern is deliberately narrow. `node_modules` and `dist` are
   * excluded explicitly because the default glob picks up compiled output in
   * `dist/` on a build, and Vitest will then try to run a test that is already
   * minified.
   */
  test: {
    environment: 'jsdom',
    globals: true,
    setupFiles: ['./src/test/setup.js'],
    include: ['src/**/*.{test,spec}.{js,jsx}'],
    exclude: ['node_modules/**', 'dist/**'],
    // A test that needs a real network or a live .NET API is not a unit test.
    // Failing fast on `fetch` is what turns "it passed in CI because the mock
    // was forgotten" into an obvious error.
    restoreMocks: true,
    clearMocks: true,
  },
  build: {
    // Emitting the manifest is what makes the bundle auditable: without it
    // there is no way to answer "what does a first-time visitor actually
    // download?" other than guessing from filenames.
    manifest: true,
    rollupOptions: {
      output: {
        /*
         * Vendor splitting.
         *
         * With route-level `lazy()` alone, Rollup produced one 707 kB chunk for
         * everything the shell shares. That chunk is downloaded on the first
         * paint, so anything large in it is paid for by every visitor -- and
         * anything that changes in it invalidates it for everyone, because
         * chunk filenames are content-hashed.
         *
         * Grouping the framework and the big libraries into their own long-lived
         * chunks means a change to app code no longer busts React, and a change
         * to React no longer busts the app code.
         *
         * `recharts` and `@paypal/react-paypal-js` are split too, and are worth
         * remembering: both are only reachable from a single page each (the
         * admin dashboard and the offer-checkout modal). They must not be pulled
         * into a shared chunk, or they become part of the first paint for
         * everyone rather than being fetched on demand.
         */
        manualChunks(id) {
          if (!id.includes('node_modules')) return undefined

          // Recharts is deliberately NOT given a manual chunk. It is reachable
          // only from the (lazy) admin dashboard, and naming it in manualChunks
          // made Rollup hoist it into the entry's static graph -- putting 327 kB
          // of charting library into first paint. Left alone, Rollup correctly
          // emits it inside the dashboard's own dynamic chunk.

          // Only needed on the checkout modal.
          if (/node_modules\/@paypal/.test(id)) return 'vendor-paypal'

          // Signature capture, also checkout-only.
          if (/node_modules\/(react-signature-canvas|trim-canvas|sign-canvas)/.test(id)) {
            return 'vendor-signature'
          }

          if (/node_modules\/(react|react-dom|scheduler)\//.test(id)) return 'vendor-react'
          if (/node_modules\/react-router/.test(id)) return 'vendor-router'
          if (/node_modules\/(@reduxjs|react-redux|redux-persist|reselect|immer)/.test(id)) {
            return 'vendor-redux'
          }
          if (/node_modules\/@tanstack/.test(id)) return 'vendor-query'
          if (/node_modules\/(framer-motion|motion-dom|motion-utils)/.test(id)) {
            return 'vendor-motion'
          }
          if (/node_modules\/(zod|@hookform|react-hook-form|@radix-ui)/.test(id)) {
            return 'vendor-forms'
          }
          if (/node_modules\/axios/.test(id)) return 'vendor-http'

          return undefined
        },
      },
    },
    // Surface oversized chunks instead of letting them ship silently.
    chunkSizeWarningLimit: 500,
  },
  plugins: [react(),
    tailwindcss()
  ],
  optimizeDeps: {
    include: [
      'react-signature-canvas',
      'prop-types',
      'trim-canvas'
    ]
  }
})
