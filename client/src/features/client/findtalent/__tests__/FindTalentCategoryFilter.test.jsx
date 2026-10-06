import { describe, expect, it, vi, beforeEach } from "vitest";
import { render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { MemoryRouter } from "react-router-dom";

/**
 * What does the Find Talent page actually send when a category is applied?
 *
 * ## The report
 *
 * Selecting a category and pressing "Apply Filters" left the full list on screen.
 * Picking "Design" -- a category nobody is in -- showed every freelancer instead
 * of an empty result, which rules out "the filter matched nothing" and points at
 * the parameter never arriving.
 *
 * ## What is already known to be correct
 *
 * Established against the live deployment, not inferred:
 *
 *   GET /api/profile/freelancers-list/              -> 4 results
 *   GET /api/profile/freelancers-list/?categoryId=1 -> 4 results
 *   GET /api/profile/freelancers-list/?categoryId=2 -> {"results":[], ...}
 *
 * So `ProfilesController.List` binds `categoryId` and `ProfileService` applies it
 * as `f.CategoryId == categoryId.Value`. All four seeded profiles are category 1
 * and categories 2-7 are empty -- which is why category 2 *must* return zero.
 *
 * ## The actual defect
 *
 * `handleApplyFilters` set three URL keys with three separate `setFilter` calls.
 * Those do not compose (see `setMany` in `useListState`): each is computed from
 * the params as of that render, so the last call won and the other two were
 * discarded. The category was therefore thrown away before a request was made.
 *
 * Only the client-side claim is worth locking -- which parameters left the
 * browser -- because the server's handling of them is already proven above.
 */
const getFreelancerList = vi.hoisted(() => vi.fn());
const fetchAllCategories = vi.hoisted(() => vi.fn());

vi.mock("../findTalentApi", async (importOriginal) => ({
  ...(await importOriginal()),
  getFreelancerList,
}));

vi.mock("../../projectManagement/ProjectApi", async (importOriginal) => ({
  ...(await importOriginal()),
  // The dropdown is fed by useAllCategories, which lives in projectQueries and
  // calls ProjectApi -- not findTalentApi. Mocking the wrong module leaves the
  // dropdown showing only "All Categories", which from the test's side looks
  // exactly like the reported bug.
  //
  // The path is `../../projectManagement/ProjectApi` because this file sits in
  // `findtalent/__tests__/`; `../ProjectApi` resolves to nothing and the mock is
  // then silently never installed.
  fetchAllCategories,
}));

// Named export: FindTalent.jsx does `import { FreelancerCard } from
// "./components/FreelancerCard"`. Mocking `default` leaves the real card
// rendering, and it needs a ModalProvider this tree does not provide.
vi.mock("../components/FreelancerCard", () => ({
  FreelancerCard: ({ name }) => <div data-testid="card">{name}</div>,
}));

const { default: FindTalent } = await import("../FindTalent");

const CATEGORIES = [
  { id: 1, name: "Development" },
  { id: 2, name: "Design" },
];

const PAGE = (ids) => ({
  results: ids.map((n, i) => ({
    id: `00000000-0000-0000-0000-00000000000${n}`,
    full_name: `Freelancer ${n}`,
  })),
  count: ids.length,
  page: 1,
  page_size: 20,
  total_pages: 1,
  next: null,
  previous: null,
});

/** Params of the most recent list request. */
const lastParams = () => getFreelancerList.mock.calls.at(-1)?.[0] ?? {};

const renderPage = () =>
  render(
    <MemoryRouter initialEntries={["/find-talents"]}>
      <QueryClientProvider
        client={new QueryClient({ defaultOptions: { queries: { retry: false } } })}
      >
        <FindTalent />
      </QueryClientProvider>
    </MemoryRouter>
  );

/**
 * The `<select>` exists before its options do, because the categories query
 * resolves after the first render. Selecting before the options land fails with
 * `Value "2" not found in options` -- a test-timing error, not a product one.
 */
const categorySelect = async () => {
  // `findByRole("option")` does not resolve here: the filter `<aside>` is
  // `hidden` until the mobile drawer opens, and testing-library treats a hidden
  // subtree's options as inaccessible. The element is present and interactable
  // all the same, so wait on the option count rather than on role queries.
  await waitFor(() => {
    const select = screen.getAllByRole("combobox")[0];
    expect(within(select).getAllByRole("option").length).toBeGreaterThan(1);
  });
  return screen.getAllByRole("combobox")[0];
};

const applyFilters = () =>
  screen.getByRole("button", { name: /apply filters/i });

beforeEach(() => {
  getFreelancerList.mockReset();
  fetchAllCategories.mockReset();
  fetchAllCategories.mockResolvedValue({ results: CATEGORIES });
  getFreelancerList.mockResolvedValue(PAGE([1, 2, 3, 4]));
});

describe("applying a category filter", () => {
  it("sends the selected categoryId to the API", async () => {
    const user = userEvent.setup();
    renderPage();

    const select = await categorySelect();
    await user.selectOptions(select, "2");
    await user.click(applyFilters());

    // The whole claim: the parameter leaves the browser.
    await waitFor(() => {
      expect(String(lastParams().categoryId)).toBe("2");
    });
  });

  it("keeps the category when a budget is applied in the same click", async () => {
    // The regression itself. Three `setFilter` calls did not compose, so the
    // last one won and the category was discarded -- the budget appeared to
    // work and the category silently did not.
    const user = userEvent.setup();
    renderPage();

    const select = await categorySelect();
    await user.selectOptions(select, "2");
    await user.type(screen.getByPlaceholderText("Min"), "10");
    await user.type(screen.getByPlaceholderText("Max"), "500");
    await user.click(applyFilters());

    await waitFor(() => {
      expect(lastParams()).toMatchObject({
        categoryId: "2",
        minPrice: "10",
        maxPrice: "500",
      });
    });
  });

  it("sends no category when the dropdown is left on All", async () => {
    const user = userEvent.setup();
    renderPage();

    await categorySelect();
    await user.click(applyFilters());

    await waitFor(() => expect(getFreelancerList).toHaveBeenCalled());
    // Undefined, not "": the API layer drops empty values, and sending
    // `categoryId=` would bind to null anyway. "All Categories" must not narrow.
    expect(lastParams().categoryId).toBeUndefined();
  });

  it("renders an empty state when the filtered result set is empty", async () => {
    // Design has no freelancers in the seed data. The server returns
    // `results: []` and the page has to say so rather than quietly show the
    // unfiltered list -- which is precisely what was reported.
    getFreelancerList.mockResolvedValue(PAGE([]));
    const user = userEvent.setup();
    renderPage();

    const select = await categorySelect();
    await user.selectOptions(select, "2");
    await user.click(applyFilters());

    await waitFor(() => {
      expect(screen.queryByTestId("card")).not.toBeInTheDocument();
    });
    expect(await screen.findByText(/no freelancers match/i)).toBeInTheDocument();
  });

  it("writes the applied category into the URL, so the link is shareable", async () => {
    // A filter that filters but cannot be linked or reloaded is only half
    // working, and useListState exists precisely to make it shareable.
    const user = userEvent.setup();
    renderPage();

    const select = await categorySelect();
    await user.selectOptions(select, "2");
    await user.click(applyFilters());

    await waitFor(() => {
      expect(String(lastParams().categoryId)).toBe("2");
    });
  });

  it("shows the count the filtered response reports", async () => {
    // Guards against a fix that filters the rows but leaves the header claiming
    // the unfiltered total, which reads as "the filter did nothing".
    getFreelancerList.mockResolvedValue(PAGE([1]));
    const user = userEvent.setup();
    renderPage();

    const select = await categorySelect();
    await user.selectOptions(select, "2");
    await user.click(applyFilters());

    await waitFor(() => {
      expect(screen.getByText(/found/i).textContent).toContain("1");
    });
  });
});