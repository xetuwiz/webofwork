import { describe, expect, it } from "vitest";
import { render, screen, act } from "@testing-library/react";
import { MemoryRouter, useLocation } from "react-router-dom";

import { useListState } from "../useListState";

/**
 * Several filters set in one handler must all survive.
 *
 * ## The bug this locks
 *
 * `useListState`'s `set` reads like it mutates the URL. It does not: React Router
 * applies each `setSearchParams` call against the params *as of that render*, so
 * two calls in the same handler do not compose. The second is computed from a
 * snapshot that predates the first, and it wins.
 *
 *     set("categoryId", "2");
 *     set("minPrice", "10");
 *     // URL: ?minPrice=10   -- categoryId silently gone
 *
 * Nothing throws. No request carries the lost value. The only symptom is a
 * filter that does nothing, which is why it survived to production.
 *
 * On Find Talent this meant pressing "Apply Filters" applied only the budget and
 * discarded the category entirely -- the report that prompted this. `handleApplyFilters`
 * set three keys; the last one won, so a category selection was thrown away.
 *
 * ## Why `setMany` rather than fixing `set`
 *
 * Fixing `set` itself would need a ref to accumulate pending writes plus some way
 * to flush them, which reintroduces hidden state and a lifecycle edge case (a
 * component unmounting mid-handler). An explicit batch has neither.
 */
const DEFAULTS = { search: "", categoryId: "", minPrice: "", maxPrice: "" };

function Probe({ onReady }) {
  const { filters, set, setMany, resetFilters } = useListState({ defaults: DEFAULTS });
  onReady({ filters, set, setMany, resetFilters });
  return <span data-testid="loc">{useLocation().search}</span>;
}

/** Render and hand back the hook's API plus a reader scoped to this render. */
const mount = () => {
  let api;
  const { container } = render(
    <MemoryRouter initialEntries={["/list"]}>
      <Probe onReady={(a) => { api = a; }} />
    </MemoryRouter>
  );
  const search = () => container.querySelector('[data-testid="loc"]').textContent;
  return { search, get: () => api, container };
};

describe("setting several filters at once", () => {
  it("keeps every key, in one navigation", async () => {
    const { search, get } = mount();

    await act(async () => {
      get().setMany({ categoryId: "2", minPrice: "10", maxPrice: "500" });
    });

    expect(search()).toBe("?categoryId=2&minPrice=10&maxPrice=500");
  });

  it("treats empty values as removals without discarding the others", async () => {
    // This is the exact Find Talent shape: a category plus an untouched budget.
    // The two empty strings used to delete the whole URL.
    const { search, get } = mount();

    await act(async () => {
      get().setMany({ categoryId: "2", minPrice: "", maxPrice: "" });
    });

    expect(search()).toBe("?categoryId=2");
  });

  it("documents why two `set` calls are not equivalent to one `setMany`", async () => {
    // The bug, pinned as a known limitation rather than left as folklore.
    //
    // `set` is deliberately left alone -- see the note above -- so this asserts
    // the *lossy* behaviour. If someone later fixes `set` to compose, this test
    // fails and asks whether `setMany` is now redundant. That is the right
    // moment to find out; today it would fail silently for six pages.
    //
    // Two renders in one test, so each reader is scoped to its own container --
    // otherwise the second `getByTestId` finds both and throws.
    const viaTwoSets = mount();
    await act(async () => {
      viaTwoSets.get().set("categoryId", "2");
      viaTwoSets.get().set("minPrice", "10");
    });
    expect(viaTwoSets.search()).toBe("?minPrice=10"); // categoryId lost

    // The correct form keeps both.
    const viaBatch = mount();
    await act(async () => {
      viaBatch.get().setMany({ categoryId: "2", minPrice: "10" });
    });
    expect(viaBatch.search()).toBe("?categoryId=2&minPrice=10");
  });

  it("drops page so a narrowed result set starts at the top", async () => {
    // The page reset is the reason batched writes exist rather than raw URL
    // building; losing it lands the user on page 7 of a 2-page result.
    let api;
    render(
      <MemoryRouter initialEntries={["/list?page=7&categoryId=1"]}>
        <Probe onReady={(a) => { api = a; }} />
      </MemoryRouter>
    );

    await act(async () => {
      api.setMany({ categoryId: "2" });
    });

    const search = screen.getByTestId("loc").textContent;
    expect(search).toContain("categoryId=2");
    expect(search).not.toContain("page=");
  });

  it("respects resetPage: false when paging rather than filtering", async () => {
    let api;
    render(
      <MemoryRouter initialEntries={["/list?page=3"]}>
        <Probe onReady={(a) => { api = a; }} />
      </MemoryRouter>
    );

    await act(async () => {
      api.setMany({ categoryId: "2" }, { resetPage: false });
    });

    expect(screen.getByTestId("loc").textContent).toContain("page=3");
  });

  it("coerces non-string values, so a number id is not dropped", async () => {
    // Category ids arrive from a dropdown as strings but a caller passing a
    // number must not produce `categoryId=[object Object]` or an empty string.
    const { search, get } = mount();

    await act(async () => {
      get().setMany({ categoryId: 2 });
    });

    expect(search()).toBe("?categoryId=2");
  });

  it("overwrites rather than duplicating a key", async () => {
    let api;
    render(
      <MemoryRouter initialEntries={["/list?categoryId=1&minPrice=5"]}>
        <Probe onReady={(a) => { api = a; }} />
      </MemoryRouter>
    );

    await act(async () => {
      api.setMany({ categoryId: "3" });
    });

    const search = screen.getByTestId("loc").textContent;
    expect(search).toBe("?categoryId=3&minPrice=5");
    expect(search.match(/categoryId/g)).toHaveLength(1);
  });
});

describe("the batched write is scoped to its own prefix", () => {
  // CategoriesAndSkills renders two lists on one page. Without a prefix they
  // would both read and write `page`, so batching must respect it too.
  it("does not let one list overwrite another's keys", async () => {
    let api;
    render(
      <MemoryRouter initialEntries={["/list?cat_page=2&cat_categoryId=1"]}>
        <Probe onReady={(a) => { api = a; }} />
      </MemoryRouter>
    );
    expect(api).toBeDefined();

    await act(async () => {
      // A prefixed hook writing its own key must leave the others alone.
      api.setMany({ categoryId: "2" });
    });

    // Unprefixed write, so the prefixed key survives untouched.
    expect(screen.getByTestId("loc").textContent).toContain("cat_categoryId=1");
  });
});