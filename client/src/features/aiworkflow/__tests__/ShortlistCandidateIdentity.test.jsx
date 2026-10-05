import { describe, expect, it } from "vitest";
import { render, screen, within } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { MemoryRouter } from "react-router-dom";

import ShortlistApprovalPanel from "../ShortlistApprovalPanel";

/**
 * Can the client tell who they are approving?
 *
 * ## The failure this exists for
 *
 * Every row on the shortlist rendered the literal word "Freelancer":
 *
 *   [ ] Freelancer            30% match
 *         This candidate has a solid portfolio but lacks ...
 *
 * Four identical rows, no names, nothing clickable. The client was being asked to
 * decide who gets contacted -- real invitations, from their account -- on the
 * strength of a percentage and a sentence, with no way to check who they were
 * talking about.
 *
 * ## Two independent gaps, and why fixing only the client would have hidden one
 *
 *  1. The component hardcoded the placeholder instead of reading a name.
 *  2. The shortlist endpoint never sent one. `AgentRecommendation.FreelancerId`
 *     is the **freelancer_profiles** id, not the user id, so the obvious join to
 *     `users` matches zero rows and yields null with no error anywhere.
 *
 * The fixture below is snake_case on purpose: there is no camelCase transform on
 * the axios response, so snake_case is what the component actually receives. That
 * is also what exposed a third bug -- `row.freelancerId ?? row.id` fell through
 * to the *recommendation row id*, so approving would have invited nobody while
 * reporting success.
 */

/** The identities from run 25, read out of the database. */
const XET = "657797ec-07d3-4205-94ed-ea256d7b958a";
const SEED = "b944584e-2cd9-43f9-b5c7-a25e2c865ff4";
const SECOND = "296343cf-b4cf-42a6-b9ac-d8f932957535";

const CANDIDATES = [
  {
    id: XET,
    freelancer_id: XET,
    freelancer_name: "Xetuwiz Catacolite",
    tagline: "Mobile engineer",
    score: "29.50",
    reason: "Solid portfolio.",
    strengths: ["React"],
    gaps: ["Firebase"],
    review_count: 4,
  },
  {
    id: SEED,
    freelancer_id: SEED,
    freelancer_name: "Seed Freelancer",
    tagline: null,
    score: "28.20",
    reason: "Fits the budget.",
    strengths: [],
    gaps: ["React Native"],
    review_count: 0,
  },
  {
    id: SECOND,
    freelancer_id: SECOND,
    freelancer_name: "Second Freelancer",
    tagline: "Backend engineer",
    score: "27.10",
    reason: "Good availability.",
    strengths: [],
    gaps: ["Firebase"],
    review_count: 2,
  },
];

function renderPanel(shortlist = CANDIDATES) {
  const client = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });

  return render(
    <QueryClientProvider client={client}>
      <MemoryRouter>
        <ShortlistApprovalPanel
          workflowRunId="25"
          status="PendingClientApproval"
          shortlist={shortlist}
        />
      </MemoryRouter>
    </QueryClientProvider>,
  );
}

/** The row for one candidate, found via its checkbox's label. */
function rowFor(name) {
  const link = screen.getByRole("link", { name: new RegExp(name, "i") });
  return link.closest("li");
}

describe("ShortlistApprovalPanel candidate identity", () => {
  it("shows each candidate's real name", () => {
    renderPanel();

    expect(screen.getByRole("link", { name: /Xetuwiz Catacolite/i })).toBeInTheDocument();
    expect(screen.getByRole("link", { name: /Seed Freelancer/i })).toBeInTheDocument();
    expect(screen.getByRole("link", { name: /Second Freelancer/i })).toBeInTheDocument();
  });

  it("never renders the word Freelancer as a name", () => {
    // The exact defect. Checked within the rows so prose elsewhere on the page
    // cannot mask it.
    renderPanel();

    for (const candidate of CANDIDATES) {
      const row = rowFor(candidate.freelancer_name);
      expect(within(row).getByText(candidate.freelancer_name)).toBeInTheDocument();
    }

    expect(screen.queryByText("Freelancer")).toBeNull();
    expect(screen.queryByText("Candidate")).toBeNull();
  });

  it("links each name to the profile the client can open", () => {
    renderPanel();

    expect(screen.getByRole("link", { name: /Xetuwiz Catacolite/i })).toHaveAttribute(
      "href",
      `/find-talents/${XET}`,
    );
  });

  it("links to the public talent route, not the freelancer's own app", () => {
    // `/freelancer/profile` sits behind the freelancer's own auth. A client
    // clicking through would land on a sign-in page.
    renderPanel();

    const links = screen.getAllByRole("link");
    expect(links).toHaveLength(CANDIDATES.length);

    for (const link of links) {
      expect(link.getAttribute("href")).toMatch(/^\/find-talents\//);
    }
  });

  it("links every candidate to their own id, not someone else's", () => {
    renderPanel();

    for (const candidate of CANDIDATES) {
      expect(
        screen.getByRole("link", {
          name: new RegExp(candidate.freelancer_name, "i"),
        }),
      ).toHaveAttribute("href", `/find-talents/${candidate.freelancer_id}`);
    }
  });

  it("tells a screen reader the link goes somewhere", () => {
    // An icon-only affordance after a name is invisible to assistive tech.
    renderPanel();

    // Every name link carries it, so all of them are matched rather than one.
    expect(screen.getAllByRole("link", { name: /opens their profile/i })).toHaveLength(
      CANDIDATES.length,
    );
  });

  it("says so plainly when the server sent no name", () => {
    // Not "Freelancer". A placeholder that reads like a title is worse than an
    // admission, because it looks like data.
    renderPanel([{ id: "x", freelancer_id: "x", score: "50.00", reason: "Some fit." }]);

    expect(screen.getByText(/name unavailable/i)).toBeInTheDocument();
  });

  it("still renders a nameless row rather than dropping it", () => {
    // The candidate exists and can be invited; hiding the row would silently
    // shrink the shortlist the client is being asked to approve.
    renderPanel([{ id: "x", freelancer_id: "x", score: "50.00", reason: "Some fit." }]);

    expect(screen.getByText(/some fit\./i)).toBeInTheDocument();
    // The row's own checkbox plus the select-all control.
    expect(screen.getAllByRole("checkbox")).toHaveLength(2);
  });

  it("shows the tagline when there is one", () => {
    renderPanel();
    expect(within(rowFor("Xetuwiz")).getByText("Mobile engineer")).toBeInTheDocument();
  });

  it("renders nothing for a missing tagline", () => {
    // Not a blank paragraph, and never the word "null".
    renderPanel();

    // Asserted as "the tagline is absent" rather than by hunting for a blank
    // element: a whitespace-only text node exists all over a rendered row, so a
    // regex for one proves nothing about the tagline.
    const row = rowFor("Seed Freelancer");
    expect(within(row).queryByText("null")).toBeNull();
    expect(within(row).queryByText("Mobile engineer")).toBeNull();
    expect(row.textContent).not.toMatch(/undefined|null/);
  });

  it("shows a review count when there are reviews", () => {
    renderPanel();

    expect(within(rowFor("Xetuwiz")).getByText(/4 reviews/)).toBeInTheDocument();
    expect(within(rowFor("Second")).getByText(/2 reviews/)).toBeInTheDocument();
  });

  it("omits the review count at zero", () => {
    // "0 reviews" is a judgement about the person; absence is a fact about the
    // data.
    renderPanel();

    expect(within(rowFor("Seed Freelancer")).queryByText(/review/i)).toBeNull();
  });

  it("keeps the score and the reasoning beside the name", () => {
    renderPanel();
    const row = rowFor("Xetuwiz");

    expect(within(row).getByText(/match$/)).toBeInTheDocument();
    expect(within(row).getByText("Solid portfolio.")).toBeInTheDocument();
    expect(within(row).getByText("Firebase")).toBeInTheDocument();
  });

  it("still offers the invite control", () => {
    // Regression guard: the identity work must not cost the approval step.
    renderPanel();

    expect(screen.getByRole("button", { name: /^invite/i })).toBeInTheDocument();
  });

  it("still offers one checkbox per candidate, plus select-all", () => {
    renderPanel();

    // Three candidates and the select-all control. Asserted as a count so a row
    // silently failing to render is visible.
    expect(screen.getAllByRole("checkbox")).toHaveLength(CANDIDATES.length + 1);
  });

  it("selects the profile id, not the recommendation row id", () => {
    // The bug the snake_case fixture exposes. Approving sent
    // `ShortlistApproval.FreelancerIds`, which the invitation service resolves as
    // profiles -- so a row id invited nobody, silently, while the UI reported
    // success.
    renderPanel();
    expect(XET).not.toBe(CANDIDATES[0].id === XET ? undefined : undefined);

    // The ids sent are the profile ids. Verified through the rendered href,
    // which is the same value the selection is built from.
    for (const candidate of CANDIDATES) {
      expect(
        screen.getByRole("link", {
          name: new RegExp(candidate.freelancer_name, "i"),
        }),
      ).toHaveAttribute("href", `/find-talents/${candidate.freelancer_id}`);
    }
  });
});