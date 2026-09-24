import { describe, expect, it } from "vitest";
import { projectCreateSchema } from "../projectSchemas.js";

describe("projectCreateSchema", () => {
  it("requires a fixed price when pricingType is fixed", () => {
    const result = projectCreateSchema.safeParse({
      title: "Landing page redesign",
      description: "This project requires a clean redesign for the marketing homepage.",
      requirements: "Modern homepage, responsive layout.",
      expectedDeliverables: "Updated homepage design and asset pack",
      pricingType: 1,
      expectedDays: 14,
      categoryId: 1,
      skillIds: [1],
      customSkillNames: [],
    });

    expect(result.success).toBe(false);
    expect(result.error.issues.some((issue) => issue.path.includes("fixedPrice"))).toBe(true);
  });

  it("requires range values to be valid for hourly/range pricing", () => {
    const result = projectCreateSchema.safeParse({
      title: "Mobile app UI system",
      description: "We need a polished, responsive UI system for the mobile storefront.",
      requirements: "Components, specs, and onboarding flow.",
      expectedDeliverables: "Full UI kit and handoff docs",
      pricingType: 2,
      minPrice: 250,
      maxPrice: 250,
      expectedDays: 18,
      categoryId: 2,
      skillIds: [2],
      customSkillNames: [],
    });

    expect(result.success).toBe(false);
    expect(result.error.issues.some((issue) => issue.path.includes("maxPrice"))).toBe(true);
  });
});
