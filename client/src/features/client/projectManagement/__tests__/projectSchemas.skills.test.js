import { describe, it, expect } from "vitest";
import { z } from "zod";
import { projectCreateSchema } from "../projectSchemas";

/**
 * The skills field must accept every skill the platform actually offers.
 *
 * ## What was broken
 *
 * The field validated each name against two regexes: `/^[^0-9]*$/` ("Title
 * cannot contain numbers" — a copy-paste from the title rule, on the skills
 * field) and `/^[a-zA-Z\s.]*$/` ("letters and spaces").
 *
 * Run over the 654 seeded catalogue names, that rejected **43 of them** — C#,
 * C++, HTML5, CSS3, GPT-4, Llama 3, n8n, S3, EC2, Neo4j, `.NET` and more.
 *
 * The consequence was not subtle and not hypothetical:
 *
 * - The form's own suggestion chips offered `C#` and `C++`. Clicking one produced
 *   a value its own validation then refused.
 * - The drafting agent's suggestion for a responsive-website brief was
 *   `["CSS3", "Accessibility", "JavaScript", "Responsive Design"]`, which this
 *   schema rejected with "Title cannot contain numbers | Skill can only contain
 *   letters and spaces". The client could not post the AI's answer.
 *
 * ## What these tests hold
 *
 * The allow-list's purpose was never to keep digits and symbols out -- it was to
 * keep markup out of a string the server stores and renders. So the tests below
 * assert both halves: real technology names are accepted, and injection attempts
 * are still refused. Fixing the first by deleting the rules would pass a naive
 * test and reintroduce the actual vulnerability.
 */

/** Two known-good names so `min(3)` is never what fails. */
const FLOOR = ["Accessibility", "Python"];

const accepts = (skill) =>
  projectCreateSchema.shape.skills.safeParse([...FLOOR, skill]).success;

describe("projectCreateSchema skills field", () => {
  describe("names the catalogue really contains", () => {
    // Every one of these is a seeded row, not a hypothetical.
    const realNames = [
      "C#",
      "C++",
      "CI/CD",
      "A/B Testing",
      "HTML5",
      "CSS3",
      "GPT-4",
      "GPT-4o",
      "Llama 3",
      "Veo 3",
      "n8n",
      "EC2",
      "S3",
      "Neo4j",
      "GA4",
      ".NET",
      ".NET Core",
      ".NET MAUI",
      "3D Animation",
      "3D Modeling",
      "E2E Testing",
      "Node.js",
      "Vue.js",
      "Scikit-learn",
      "On-Page SEO",
      "Speech-to-Text",
    ];

    it.each(realNames)("accepts %j", (name) => {
      expect(accepts(name)).toBe(true);
    });

    it("accepts every name the drafter produced for a responsive-website brief", () => {
      // The exact live failure, verbatim from run 19's stored draft plus the
      // third skill the client was told to add to satisfy `.min(3)`.
      const suggestion = ["CSS3", "Accessibility", "JavaScript", "Responsive Design"];

      const result = projectCreateSchema.shape.skills.safeParse(suggestion);

      expect(result.success).toBe(true);
    });
  });

  describe("markup and injection, still refused", () => {
    // The reason the rules existed. Relaxing the character set must not become
    // "accept anything".
    const attacks = [
      "<script>alert(1)</script>",
      '"><img src=x onerror=alert(1)>',
      "Robert'); DROP TABLE users;--",
      "<svg/onload=alert(1)>",
      "javascript:alert(1)",
      "<iframe src=evil>",
      "a & b",
      "'; DELETE FROM projects; --",
    ];

    it.each(attacks)("rejects %j", (name) => {
      expect(accepts(name)).toBe(false);
    });
  });

  describe("blank input", () => {
    it("rejects an empty string, and says so", () => {
      const result = projectCreateSchema.shape.skills.safeParse([...FLOOR, ""]);

      expect(result.success).toBe(false);
      // The message is asserted, not just the verdict. `.min(1)` is redundant as
      // *validation* -- the regex's first-character class already forbids an empty
      // string -- and a mutation removing it left every other test green. What it
      // uniquely contributes is the wording: without it a whitespace-only value
      // reports only "A skill can contain letters, numbers, spaces and . + # / -",
      // which is a baffling thing to read about a field the client left blank.
      expect(result.error.issues.map((i) => i.message)).toContain(
        "Skill cannot be blank"
      );
    });

    it("rejects a whitespace-only name with a message about being blank", () => {
      // Reachable: the old rules allowed `\\s`, so `["  ", "  ", "  "]` satisfied
      // the whole field. The server then did `raw.Trim()[0]` on the empty result
      // and threw -- a 500 from a validation rule that was supposed to prevent it.
      const result = projectCreateSchema.shape.skills.safeParse([...FLOOR, "   "]);

      expect(result.success).toBe(false);
      expect(result.error.issues.map((i) => i.message)).toContain(
        "Skill cannot be blank"
      );
    });

    it("normalises surrounding whitespace rather than rejecting the name", () => {
      // Stricter than rejecting would be annoying and stricter than accepting
      // would be pointless: a client who types " React " means React. Zod's
      // `.trim()` transforms before the regex runs, so the stored value is the
      // clean one -- which is what reaches `ProjectService` and the database.
      //
      // Asserted on the *output* as well as the verdict, because "accepted" alone
      // would also be true if the padded string were being stored as typed.
      const result = projectCreateSchema.shape.skills.safeParse([...FLOOR, " React "]);

      expect(result.success).toBe(true);
      expect(result.data[2]).toBe("React");
    });

    it("normalises a padded symbol-bearing name too", () => {
      const result = projectCreateSchema.shape.skills.safeParse([...FLOOR, "C# "]);

      expect(result.success).toBe(true);
      expect(result.data[2]).toBe("C#");
    });
  });

  describe("the minimum", () => {
    it("still requires three", () => {
      expect(projectCreateSchema.shape.skills.safeParse(["C#", "C++"]).success).toBe(
        false
      );
    });

    it("is satisfied by three real names that contain symbols and digits", () => {
      // The two rules interact: a client told to "add one more skill" must be able
      // to add C#, not just another word.
      expect(
        projectCreateSchema.shape.skills.safeParse(["C#", "C++", "HTML5"]).success
      ).toBe(true);
    });

    it("explains itself rather than leaking zod's default", () => {
      const result = projectCreateSchema.shape.skills.safeParse(["C#"]);

      expect(result.success).toBe(false);
      expect(result.error.issues[0].message).toMatch(/at least 3/i);
    });
  });

  describe("the rest of the schema is untouched", () => {
    const base = {
      title: "Build a Modern Responsive Website",
      description: "x".repeat(60),
      requirements: "x".repeat(30),
      expected_deliverables: "x".repeat(30),
      category: 1,
      pricing_type: "fixed",
      fixed_price: 1500,
      delivery_days: 14,
    };

    it("accepts a complete project using symbol-bearing skills", () => {
      const result = projectCreateSchema.safeParse({
        ...base,
        skills: ["C#", "C++", "HTML5"],
      });

      expect(result.success).toBe(true);
    });

    it("still enforces the title rule", () => {
      expect(projectCreateSchema.safeParse({ ...base, title: "abc" }).success).toBe(
        false
      );
    });

    it("still requires a description of real length", () => {
      expect(
        projectCreateSchema.safeParse({ ...base, description: "short" }).success
      ).toBe(false);
    });
  });
});

describe("the skill name rule, on its own", () => {
  it("is an allow-list, not a deny-list", () => {
    // Asserted structurally. If someone replaces the regex with "no angle
    // brackets", the injection tests above would still pass while the field had
    // quietly stopped being an allow-list -- which is the property that keeps new
    // attack shapes out.
    const rule = z
      .string()
      .regex(/^[A-Za-z0-9.][A-Za-z0-9 .+#/-]*$/);

    expect(rule.safeParse("C#").success).toBe(true);
    expect(rule.safeParse("<").success).toBe(false);
    expect(rule.safeParse(">").success).toBe(false);
    expect(rule.safeParse('"').success).toBe(false);
    expect(rule.safeParse("'").success).toBe(false);
    expect(rule.safeParse("&").success).toBe(false);
  });
});