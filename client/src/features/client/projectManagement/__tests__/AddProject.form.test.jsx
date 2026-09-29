import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import AddProject from "../AddProject";

/**
 * The form, rendered for real.
 *
 * Everything else in this directory tests a piece of logic in isolation. That
 * left the one thing nobody had checked: whether the pieces still agree with each
 * other once they are in the same component.
 *
 * ## Why that mattered
 *
 * `applyDraft` fills skills and then fills the category. A separate `useEffect`
 * cleared `skills` whenever `category` changed. Both were individually
 * reasonable and the composition was silent data loss: the draft's skills were
 * written, then wiped a render later, while the "filled in by the AI" badge --
 * driven off a separate `aiFilled` set -- stayed on. So the form claimed the AI
 * had supplied skills while showing none.
 *
 * No isolated unit test can see that, because the bug is entirely in the
 * ordering between two correct-looking pieces of code.
 */

const CATEGORIES = [
  { id: 1, name: "Web Development" },
  { id: 2, name: "Data Science" },
];

const SKILLS = [
  { id: 10, name: "HTML5", category: 1 },
  { id: 11, name: "CSS3", category: 1 },
  { id: 12, name: "JavaScript", category: 1 },
  { id: 20, name: "Python", category: 2 },
  { id: 21, name: "Pandas", category: 2 },
];

/** The draft the agent returns for a responsive-site brief. */
const DRAFT = {
  raw: JSON.stringify({
    title: "Build a responsive marketing site",
    description: "A marketing site that works on every screen size.",
    requirements: "Handed over as source and as a build.",
    expected_deliverables: "Deployed site and repository.",
    category_id: 1,
    skill_names: ["HTML5", "CSS3", "JavaScript"],
    pricing_type: "fixed",
    fixed_price_usd: 2500,
    delivery_days: 21,
  }),
};

const toastError = vi.fn();
const toastSuccess = vi.fn();

/** Pushes a draft into the form the way the real button does. */
let deliverDraft;
const DraftAssistButton = ({ partialText, onApply }) => (
  <button
    type="button"
    onClick={() => onApply(deliverDraft({ ...DRAFT, partialText }))}
    data-testid="help-me-draft"
  >
    Help me draft
  </button>
);

vi.mock("../projectQueries", () => ({
  useAllSkillls: () => ({ data: SKILLS }),
}));

vi.mock("../../../admin/categorymanagement/categoryQueries", () => ({
  useAllCategories: () => ({ data: CATEGORIES }),
}));

vi.mock("../projectMutations", () => ({
  useCreateProject: () => ({ mutateAsync: vi.fn(), isPending: false }),
}));

vi.mock("../../../../hooks/modal/useModalStore", () => ({
  useModal: () => ({ openModal: vi.fn(), closeModal: vi.fn() }),
}));

vi.mock("../../../../hooks/useUnsavedChangesGuard", () => ({
  useFormUnsavedChangesGuard: () => {},
}));

vi.mock("../../../../utils/toast", () => ({
  toastError: (...a) => toastError(...a),
  toastSuccess: (...a) => toastSuccess(...a),
}));

vi.mock("react-router-dom", () => ({
  useNavigate: () => vi.fn(),
}));

vi.mock("../../../aiworkflow/DraftAssistButton", () => ({
  default: (props) => <DraftAssistButton {...props} />,
}));

vi.mock("../../../aiworkflow/AiFieldBadge", () => ({
  default: ({ show, children }) => (show ? <span data-testid="ai-badge">{children}</span> : null),
  AiFieldEdited: ({ show, children }) => (show ? <span data-testid="edited-badge">{children}</span> : null),
}));

/** The skills the form is currently showing as chips. */
function chips() {
  return screen
    .queryAllByRole("button", { name: /^Remove / })
    .map((b) => b.getAttribute("aria-label").replace("Remove ", ""));
}

function skillsField() {
  return screen.getByRole("group", { name: /Required Skills/i });
}

function typeSkill(user, name) {
  const input = within(skillsField()).getByPlaceholderText("Type a skill");
  return user.type(input, name).then(() => user.click(within(skillsField()).getByRole("button", { name: "Add" })));
}

function pickCategory(user, name) {
  return user.selectOptions(screen.getByLabelText(/^Category/), name);
}

beforeEach(() => {
  toastError.mockClear();
  toastSuccess.mockClear();
  deliverDraft = () => {};
});

describe("adding a project by hand", () => {
  it("keeps the skills the client typed", async () => {
    const user = userEvent.setup();
    render(<AddProject />);

    await typeSkill(user, "HTML5");
    await typeSkill(user, "CSS3");
    await typeSkill(user, "JavaScript");

    expect(chips()).toEqual(["HTML5", "CSS3", "JavaScript"]);
  });

  it("keeps them when the client later changes the category", async () => {
    // The bug. `selectedCategory` feeding an effect that cleared `skills` meant
    // picking a category after adding skills threw the skills away with no
    // warning -- including on a category the client had not finished choosing.
    const user = userEvent.setup();
    render(<AddProject />);

    await pickCategory(user, "Web Development");
    await typeSkill(user, "HTML5");
    await typeSkill(user, "CSS3");

    await pickCategory(user, "Data Science");

    expect(chips()).toEqual(["HTML5", "CSS3"]);
  });

  it("keeps a hand-typed skill that is not in the catalogue", async () => {
    // Custom names are a supported feature, not a validation failure.
    const user = userEvent.setup();
    render(<AddProject />);

    await typeSkill(user, "Terraform");

    expect(chips()).toEqual(["Terraform"]);
  });

  it("refuses a duplicate that differs only in case", async () => {
    const user = userEvent.setup();
    render(<AddProject />);

    await typeSkill(user, "HTML5");
    await typeSkill(user, "html5");

    expect(chips()).toEqual(["HTML5"]);
  });

  it("removes a chip when its × is pressed", async () => {
    const user = userEvent.setup();
    render(<AddProject />);

    await typeSkill(user, "HTML5");
    await typeSkill(user, "CSS3");
    await user.click(screen.getByRole("button", { name: "Remove HTML5" }));

    expect(chips()).toEqual(["CSS3"]);
  });

  it("keeps the skills when the category is put back to unselected", async () => {
    // The mirror of the same bug. Changing category was never the trigger; the
    // trigger was *the category changing at all*, so backing the <select> out to
    // "Select a category" -- which is just one more change -- destroyed the work
    // the same way. Worth its own test because the obvious fix for the first one
    // (invert the condition) reintroduces this one.
    const user = userEvent.setup();
    render(<AddProject />);

    await pickCategory(user, "Web Development");
    await typeSkill(user, "HTML5");
    await typeSkill(user, "CSS3");

    await pickCategory(user, "");

    expect(chips()).toEqual(["HTML5", "CSS3"]);
  });

  it("offers only the new category's suggestions after a switch", async () => {
    // Clearing skills was presumably meant to keep the form consistent with the
    // category. The suggestions already do that, and doing it to the chosen
    // skills too was collateral damage.
    const user = userEvent.setup();
    render(<AddProject />);

    await pickCategory(user, "Web Development");
    await user.type(within(skillsField()).getByPlaceholderText("Type a skill"), "a");

    const webSuggestions = within(skillsField()).getAllByRole("button", { name: /^\+ / }).map((b) => b.textContent);
    expect(webSuggestions).toContain("+ JavaScript");

    await pickCategory(user, "Data Science");

    const dataSuggestions = within(skillsField()).getAllByRole("button", { name: /^\+ / }).map((b) => b.textContent);
    expect(dataSuggestions).toEqual(["+ Pandas"]);
  });
});

describe("applying the agent's draft", () => {
  it("puts the draft's skills on the form as chips", async () => {
    const user = userEvent.setup();
    deliverDraft = (d) => d;
    render(<AddProject />);

    await user.click(screen.getByTestId("help-me-draft"));

    await waitFor(() => expect(chips()).toEqual(["HTML5", "CSS3", "JavaScript"]));
  });

  it("still has the skills a moment later, once the category effect has run", async () => {
    // The exact failure that was live: the chips appeared, then a re-render
    // triggered by the category the same draft had just filled removed them.
    const user = userEvent.setup();
    deliverDraft = (d) => d;
    render(<AddProject />);

    await user.click(screen.getByTestId("help-me-draft"));
    await waitFor(() => expect(chips()).toHaveLength(3));

    // A second beat: effects have flushed, re-renders have settled.
    await new Promise((r) => setTimeout(r, 50));

    expect(chips()).toEqual(["HTML5", "CSS3", "JavaScript"]);
  });

  it("fills the category too", async () => {
    const user = userEvent.setup();
    deliverDraft = (d) => d;
    render(<AddProject />);

    await user.click(screen.getByTestId("help-me-draft"));

    await waitFor(() =>
      expect(screen.getByLabelText(/^Category/)).toHaveValue("1")
    );
  });

  it("merges with skills the client added first, rather than replacing them", async () => {
    const user = userEvent.setup();
    deliverDraft = (d) => d;
    render(<AddProject />);

    await typeSkill(user, "Terraform");
    await user.click(screen.getByTestId("help-me-draft"));

    await waitFor(() => expect(chips()).toContain("Terraform"));
    expect(chips()).toEqual(["Terraform", "HTML5", "CSS3", "JavaScript"]);
  });

  it("says which proposed skills are not on the platform, and keeps the rest", async () => {
    const user = userEvent.setup();
    deliverDraft = (d) => ({
      ...d,
      raw: JSON.stringify({
        ...JSON.parse(d.raw),
        skill_names: ["HTML5", "Underwater Basket Weaving"],
      }),
    });
    render(<AddProject />);

    await user.click(screen.getByTestId("help-me-draft"));

    await waitFor(() => expect(chips()).toEqual(["HTML5"]));
    expect(toastError).toHaveBeenCalledWith(
      expect.stringContaining("Underwater Basket Weaving")
    );
  });

  it("still leaves a hand-filled form alone", async () => {
    const user = userEvent.setup();
    deliverDraft = (d) => d;
    render(<AddProject />);

    await user.type(screen.getByPlaceholderText(/e\.g\./), "My own title");
    await user.click(screen.getByTestId("help-me-draft"));

    await waitFor(() => expect(screen.getByPlaceholderText(/e\.g\./)).toHaveValue("My own title"));
  });
});
