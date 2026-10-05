import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";
import { fileURLToPath } from "node:url";
import { dirname, resolve } from "node:path";

const here = dirname(fileURLToPath(import.meta.url));
const src = resolve(here, "../src");

/**
 * A resolver plugin rather than `resolve.alias`.
 *
 * Alias replacements are spliced into the *importing file's* relative path, so
 * one replacement string cannot serve several importers: aliasing
 * `profileQueries$` to `ssr-check/fixtures.jsx` turned ProfileEdit's
 * `../../client/projectManagement/projectQueries` into
 * `../../client/projectManagement/ssr-check/fixtures.jsx`. Absolute paths fail
 * too -- they get a `./` prepended. Matching on the bare specifier and
 * returning an absolute id sidesteps both.
 */
function fixturesPlugin() {
  const map = [
    [/react-router-dom$/, resolve(here, "stubs/router.jsx")],
    [/(project|profile)Queries$/, resolve(here, "fixtures.jsx")],
    [/(projectMutations|profileMutation)$/, resolve(here, "fixtures.jsx")],
    [/PortfolioModal$/, resolve(here, "stubs/empty.jsx")],
    // The onboarding steps all render inside this layout and read the wizard
    // from a context. Both are pure chrome for the label check, so they are
    // stubbed rather than growing the fixture list per step.
    [/OnBoardingLayout$/, resolve(here, "stubs/empty.jsx")],
    [/OnBoardingContext$/, resolve(here, "fixtures.jsx")],
    [/onBoardingQueries$/, resolve(here, "fixtures.jsx")],
    // StepTwo imports useAllCategories from the admin category queries, a
    // different module from the project one. Unaliased, the real react-query
    // hook runs and throws "No QueryClient set" -- which the harness correctly
    // reported rather than scoring as a pass.
    [/categoryQueries$/, resolve(here, "fixtures.jsx")],
    [/onBoardingMutations$/, resolve(here, "fixtures.jsx")],
    [/useModalStore$/, resolve(here, "fixtures.jsx")],
    [/cloudinaryHelper$/, resolve(here, "fixtures.jsx")],
  ];
  return {
    name: "ssr-fixtures",
    enforce: "pre",
    resolveId(source) {
      for (const [pattern, id] of map) {
        if (pattern.test(source)) return id;
      }
      return null;
    },
  };
}

export default defineConfig({
  plugins: [react(), fixturesPlugin()],
  build: {
    ssr: resolve(here, "entry.jsx"),
    outDir: resolve(here, "out"),
    emptyOutDir: true,
    rollupOptions: { external: ["react", "react-dom", "react-dom/server", "react-hook-form"] },
  },
});
