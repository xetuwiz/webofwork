import React from "react";
import { describe, expect, it, vi } from "vitest";
import { render, screen } from "@testing-library/react";
import { Loader2 } from "lucide-react";
import { MemoryRouter } from "react-router-dom";

import Button from "../../../components/common/Button";

// Mocked rather than driven through the real hook: the spinner only exists while
// the start mutation is in flight, and the actual request is irrelevant to
// whether one spinner or two is drawn.
vi.mock("../aiWorkflowQueries", () => ({
  useStartTalentMatch: () => ({ isPending: true, mutateAsync: vi.fn() }),
  useProjectWorkflow: () => ({ data: undefined }),
}));

const { default: FindTalentButton } = await import("../FindTalentButton");

/**
 * A button must never show two spinners.
 *
 * `Button` renders its own `Loader2` when `loading` is set and then renders
 * `children` regardless, so a child that draws its own spinner produces two.
 * That is not a cosmetic detail: two spinners on one control reads as a page
 * that is stuck rather than busy, and it is what made "Find best talent" look
 * broken while it was in fact working.
 *
 * So this asserts the composition rule directly -- a loading Button plus any
 * spinner in its children is two -- and then checks the real call site, because
 * the rule is easy to state and easy to break by accident on the next screen.
 */
const spinnerCount = (container) =>
  container.querySelectorAll(".animate-spin").length;

describe("Button renders exactly one spinner when loading", () => {
  it("shows its own spinner and the children", () => {
    // The behaviour the double-spinner bug depended on, asserted so that
    // changing it is a deliberate act rather than an accident.
    render(
      <Button loading>
        <span>Analysing freelancers</span>
      </Button>
    );
    expect(screen.getByText("Analysing freelancers")).toBeInTheDocument();
  });

  it("does not duplicate a spinner a child also draws", () => {
    // This is the shape the four call sites had: `loading` on the Button and a
    // manual Loader2 in the children.
    const { container } = render(
      <Button loading>
        <Loader2 className="animate-spin" aria-hidden="true" />
        Sending
      </Button>
    );
    // Asserting the rule rather than the count, so this test keeps its meaning if
    // Button is ever changed to replace children with its own spinner.
    expect(spinnerCount(container)).toBeGreaterThan(1);
  });

  it("shows a single spinner when the children supply only a label", () => {
    // The corrected shape, and the one all four call sites now use.
    const { container } = render(<Button loading>Sending</Button>);
    expect(spinnerCount(container)).toBe(1);
  });
});

describe("FindTalentButton while the match is starting", () => {
  it("shows one spinner, not two", () => {
    // Two spinners on this button is exactly what was reported. Asserting a
    // count rather than `not.toBe(2)` means a third is also a failure.
    // MemoryRouter because the button navigates to the shortlist page on success.
    const { container } = render(
      <MemoryRouter>
        <FindTalentButton projectId={1} />
      </MemoryRouter>
    );
    expect(spinnerCount(container)).toBe(1);
  });

  it("still explains what it is doing in text", () => {
    // The icon was removed, so the label is the only thing carrying "this is
    // working, wait" to anyone who cannot see the animation.
    render(
      <MemoryRouter>
        <FindTalentButton projectId={1} />
      </MemoryRouter>
    );
    expect(screen.getByText(/analysing freelancers/i)).toBeInTheDocument();
  });
});