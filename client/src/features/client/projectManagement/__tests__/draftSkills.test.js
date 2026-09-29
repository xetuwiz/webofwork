import { describe, it, expect } from "vitest";
import { selectDraftSkills } from "../draftSkills";

/**
 * Does the AI's answer actually reach the form?
 *
 * The draft panel listing a skill and the form showing it as a chip are two
 * different code paths reading the same `skill_names`. The panel joins them into
 * a string; the form has to match each one against the catalogue it loaded and
 * write the survivors into react-hook-form. Only the second one is a filter, and
 * a filter can quietly drop everything.
 *
 * The names below are the real seeded ones -- the two that appeared in the live
 * draft for a responsive-website brief, and the ones the client's own suggestion
 * chips offer.
 */

/** What `/api/categories/skills/?no_pagination=true` actually returns. */
const CATALOGUE = [
  "HTML5",
  "CSS3",
  "JavaScript",
  "Responsive Design",
  "Accessibility",
  "Python",
  "React",
  "C#",
  "C++",
  "TypeScript",
  ".NET",
  "Node.js",
];

describe("selectDraftSkills", () => {
  describe("the live draft", () => {
    it("carries every skill the agent chose into the form", () => {
      // Exactly what run 19 produced, after the minimum was satisfied.
      const chosen = ["HTML5", "CSS3", "JavaScript", "Responsive Design"];

      const { merged, added } = selectDraftSkills(chosen, [], CATALOGUE);

      expect(merged).toEqual(chosen);
      expect(added).toEqual(chosen);
    });

    it("carries the two-skill draft too, rather than dropping it for being short", () => {
      // The original failure: two real skills, and the form refused to submit.
      // Whether two is enough is the minimum's business, not this function's --
      // its only job is to lose nothing.
      const chosen = ["CSS3", "Accessibility"];

      const { merged } = selectDraftSkills(chosen, [], CATALOGUE);

      expect(merged).toEqual(chosen);
    });
  });

  describe("casing", () => {
    // The exact-match-only version dropped these. The model's casing is not
    // something the form controls, and losing a skill silently is
    // indistinguishable from it not existing.
    it.each([
      ["javascript", "JavaScript"],
      ["JAVASCRIPT", "JavaScript"],
      ["css3", "CSS3"],
      ["react", "React"],
      ["  React  ", "React"],
    ])("accepts %j and stores the catalogue's spelling %j", (proposed, expected) => {
      const { merged } = selectDraftSkills([proposed], [], CATALOGUE);

      expect(merged).toEqual([expected]);
    });

    it("keeps the catalogue's casing for symbol-bearing names", () => {
      // `.net` must not become what is typed; `ProjectService` matches
      // case-insensitively but the chip is what a freelancer reads.
      const { merged } = selectDraftSkills([".net"], [], CATALOGUE);

      expect(merged).toEqual([".NET"]);
    });
  });

  describe("names the platform does not have", () => {
    it("drops them and reports them, rather than dropping them silently", () => {
      const { merged, rejected } = selectDraftSkills(
        ["React", "Underwater Basket Weaving"],
        [],
        CATALOGUE
      );

      expect(merged).toEqual(["React"]);
      expect(rejected).toEqual(["Underwater Basket Weaving"]);
    });

    it("keeps the good ones when one is bad", () => {
      const { merged, added } = selectDraftSkills(
        ["CSS3", "Blockchain", "JavaScript"],
        [],
        CATALOGUE
      );

      expect(merged).toEqual(["CSS3", "JavaScript"]);
      expect(added).toHaveLength(2);
    });
  });

  describe("merging with what the client already chose", () => {
    it("never removes a skill the client picked", () => {
      // A draft is a suggestion. The client is the author.
      const { merged } = selectDraftSkills(["React"], ["Python", "Go"], CATALOGUE.concat("Go"));

      expect(merged).toEqual(["Python", "Go", "React"]);
    });

    it("does not duplicate a skill already on the form", () => {
      const { merged, added } = selectDraftSkills(["React", "React"], ["React"], CATALOGUE);

      expect(merged).toEqual(["React"]);
      expect(added).toEqual([]);
    });

    it("reports nothing added when the draft is entirely redundant", () => {
      // The `applyDraft` guard keys off `added.length`, so this is what stops a
      // second press from claiming the AI filled a field it did not.
      const { added } = selectDraftSkills(["React"], ["React"], CATALOGUE);

      expect(added).toEqual([]);
    });

    it("resolves a duplicate that differs only in case", () => {
      const { merged } = selectDraftSkills(["javascript"], ["JavaScript"], CATALOGUE);

      expect(merged).toEqual(["JavaScript"]);
    });
  });

  describe("malformed input", () => {
    it.each([
      [undefined, "missing"],
      [null, "null"],
      ["React", "a bare string"],
      [[], "empty"],
      [[null, 42, {}, ""], "junk entries"],
    ])("survives %s (%s) without throwing", (input) => {
      expect(() => selectDraftSkills(input, [], CATALOGUE)).not.toThrow();
    });

    it("treats a non-array proposal as nothing proposed", () => {
      const { merged, added } = selectDraftSkills(undefined, [], CATALOGUE);

      expect(merged).toEqual([]);
      expect(added).toEqual([]);
    });

    it("reports an empty catalogue as its own case, not as rejection", () => {
      // The real hazard. `skillsData` is `skillsResponse ?? []`, so it is empty
      // before the query resolves and stays empty if the request fails.
      //
      // Reporting these as "not a skill on this platform" would be a confident
      // claim about the catalogue that nothing checked -- and it would fire as a
      // toast naming the client's own skills as invalid. So an empty catalogue
      // returns `catalogueEmpty` and claims nothing either way.
      const { merged, added, rejected, catalogueEmpty } = selectDraftSkills(
        ["React", "CSS3"],
        [],
        []
      );

      expect(catalogueEmpty).toBe(true);
      expect(rejected).toEqual([]);
      expect(merged).toEqual([]);
      expect(added).toEqual([]);
    });

    it("keeps the client's existing skills when the catalogue is unavailable", () => {
      const { merged } = selectDraftSkills(["React"], ["Python"], []);

      expect(merged).toEqual(["Python"]);
    });

    it("does not claim the catalogue is empty when it simply has one entry", () => {
      const { catalogueEmpty } = selectDraftSkills(["React"], [], ["React"]);

      expect(catalogueEmpty).toBe(false);
    });
  });
});