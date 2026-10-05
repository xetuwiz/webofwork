import js from '@eslint/js'
import globals from 'globals'
import reactHooks from 'eslint-plugin-react-hooks'
import reactRefresh from 'eslint-plugin-react-refresh'
import { defineConfig, globalIgnores } from 'eslint/config'

export default defineConfig([
  // `ssr-check` is a render-verification harness, not app code. `out/` is a
  // generated bundle and `stubs/` deliberately re-exports placeholder hooks, so
  // linting either reports noise that has nothing to do with the app. Including
  // them added 17 problems to a 77-problem baseline and made a real change
  // indistinguishable from harness churn.
  globalIgnores(['dist', 'ssr-check/**']),
  {
    files: ['**/*.{js,jsx}'],
    extends: [
      js.configs.recommended,
      reactHooks.configs.flat.recommended,
      reactRefresh.configs.vite,
    ],
    languageOptions: {
      ecmaVersion: 2020,
      globals: globals.browser,
      parserOptions: {
        ecmaVersion: 'latest',
        ecmaFeatures: { jsx: true },
        sourceType: 'module',
      },
    },
    rules: {
      // `argsIgnorePattern` is needed as well as `varsIgnorePattern`: a
      // destructured function parameter ({ icon: Icon }) is reported by
      // no-unused-vars as an *arg*, not a var, so `varsIgnorePattern` alone left
      // every component like ManagementList flagged with a false
      // "'Icon' is defined but never used" even though it is rendered as <Icon />.
      'no-unused-vars': [
        'error',
        { varsIgnorePattern: '^[A-Z_]', argsIgnorePattern: '^[A-Z_]' },
      ],
    },
  },
])
