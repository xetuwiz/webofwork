import { describe, it, expect } from "vitest";
import { weightCaption } from "../weightCaption";

/**
 * What the weights panel claims about where its numbers came from.
 *
 * ## The failure this exists for
 *
 * The shortlist page rendered the panel's defaults and captioned them
 * "The AI has not chosen its weights yet. These are the defaults it starts from."
 *
 * The AI had chosen them. Run 23's planning step succeeded and wrote
 * `skill_coverage 0.4`, `portfolio_relevance 0.25` and the rest into the step log.
 * The endpoint read `WorkflowRun.Plan`, which is written only when the *client*
 * supplies a plan — null on every first run — so `fromServer` was false and the
 * panel said nothing had been chosen.
 *
 * That caption is not a cosmetic slip. It tells the client the scores below it
 * came from nowhere, which is the one thing the panel exists to prevent: a number
 * with no stated provenance is a number nobody should act on.
 */

const NO_PLAN = { fromServer: false };

describe("weightCaption", () => {
  describe("when the planner chose the weights", () => {
    it("says the AI chose them", () => {
      expect(weightCaption({ fromServer: true }, "planner")).toBe(
        "These are the weights the AI chose for this shortlist.",
      );
    });

    it("does not claim the client is looking at defaults", () => {
      // The exact sentence the live page showed. Asserting its absence is the
      // assertion that matters; the positive case alone would also pass against
      // a caption that said both.
      expect(weightCaption({ fromServer: true }, "planner")).not.toContain(
        "defaults",
      );
    });

    it("does not imply the client reviewed them", () => {
      // Presenting planner-chosen weights with "you set" is the specific
      // dishonesty the panel exists to avoid.
      expect(weightCaption({ fromServer: true }, "planner")).not.toContain("you set");
    });
  });

  describe("when the client set the weights", () => {
    it("says so", () => {
      expect(weightCaption({ fromServer: true }, "supplied")).toBe(
        "These are the weights you set for this shortlist.",
      );
    });

    it("does not attribute them to the AI", () => {
      expect(weightCaption({ fromServer: true }, "supplied")).not.toContain("AI chose");
    });
  });

  describe("when the plan arrived with no source", () => {
    it("claims only what is true of it", () => {
      // A plan exists; where it came from is unknown. Inventing an attribution
      // would be the same dishonesty in the other direction.
      expect(weightCaption({ fromServer: true }, null)).toBe(
        "These are the weights this shortlist was produced with.",
      );
    });

    it("says neither the AI nor the client chose them", () => {
      const caption = weightCaption({ fromServer: true }, null);
      expect(caption).not.toContain("AI chose");
      expect(caption).not.toContain("you set");
    });

    it("does not fall back to the defaults caption", () => {
      // The panel has a plan. Saying it does not is the bug.
      expect(weightCaption({ fromServer: true }, null)).not.toContain(
        "has not chosen its weights yet",
      );
    });
  });

  describe("when there is no plan at all", () => {
    it("says the weights are the defaults", () => {
      expect(weightCaption(NO_PLAN, null)).toBe(
        "The AI has not chosen its weights yet. These are the defaults it starts from.",
      );
    });

    it("does not claim any source, whatever the server said", () => {
      // `fromServer` is what the server's answer reduced to on the client. A
      // source arriving without a plan is inconsistent, and the honest reading
      // of "no weights came back" has to win over the label.
      expect(weightCaption(NO_PLAN, "planner")).toContain("defaults");
      expect(weightCaption(NO_PLAN, "supplied")).toContain("defaults");
    });
  });

  describe("a source this panel does not recognise", () => {
    it("falls back to the neutral caption rather than guessing", () => {
      // A future writer labelling its plans differently should not be shown to
      // the client as planner-chosen.
      expect(weightCaption({ fromServer: true }, "human_review")).toBe(
        "These are the weights this shortlist was produced with.",
      );
    });

    it("does not treat an unknown source as supplied", () => {
      expect(weightCaption({ fromServer: true }, "whatever")).not.toContain("you set");
    });
  });
});