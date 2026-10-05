import React from "react";
import { describe, expect, it } from "vitest";
import { render, screen } from "@testing-library/react";

import AiFieldBadge, { AiFieldEdited } from "../AiFieldBadge";

/**
 * The badge that marks an AI-filled field.
 *
 * The behaviour worth protecting is the *disappearing*. A badge reading
 * "filled in by the AI" is a claim about the current contents of that field, and
 * once the client types in it the contents are theirs -- leaving the badge would
 * assert something untrue, and worse, would suggest they had not looked.
 *
 * So the parent only renders it while the field is untouched, and these tests
 * check that a field with no claim at all renders nothing at all, rather than an
 * empty container holding space where a badge would go.
 */
describe("AiFieldBadge", () => {
  it("says the field was filled by the AI", () => {
    render(<AiFieldBadge show />);

    expect(screen.getByText(/filled in by the ai/i)).toBeInTheDocument();
  });

  it("tells the client they can change it, because they can", () => {
    // A badge that only informs without inviting disagreement would be a notice,
    // not a control.
    render(<AiFieldBadge show />);
    expect(screen.getByText(/change anything you disagree with/i)).toBeInTheDocument();
  });

  it("renders nothing at all when the field is not AI-filled", () => {
    // Not a hidden element or an empty <p>: nothing, so no gap in the layout and
    // nothing for a screen reader to announce.
    const { container } = render(<AiFieldBadge show={false} />);
    expect(container).toBeEmptyDOMElement();
  });

  it("is announced, not merely drawn", () => {
    // A screen-reader user filling this form has no other way to learn the
    // starting content was not their own.
    render(<AiFieldBadge show />);
    expect(screen.getByRole("status")).toBeInTheDocument();
  });

  it("does not offer an accept button, because there is nothing to accept", () => {
    // The draft has already been written into the form. An Accept control would
    // imply a pending state that does not exist.
    render(<AiFieldBadge show />);
    expect(screen.queryByRole("button")).not.toBeInTheDocument();
  });
});

describe("AiFieldEdited", () => {
  it("says the field was edited from the suggestion", () => {
    render(<AiFieldEdited show />);
    expect(screen.getByText(/edited from the ai/i)).toBeInTheDocument();
  });

  it("renders nothing when the field was never AI-filled", () => {
    const { container } = render(<AiFieldEdited show={false} />);
    expect(container).toBeEmptyDOMElement();
  });
});
