import { describe, expect, it } from "vitest";
import OnboardingManager from "../OnboardingManager.jsx";
import { stepOneSchema } from "../step1/stepOneSchema.js";
import { stepTwoSchema } from "../step2/stepTwoSchema.js";
import { stepThreeSchema } from "../step3/stepThreeSchema.js";
import { stepFourSchema } from "../step4/stepFourSchema.js";
import { stepFiveSchema } from "../step5/stepFiveSchema.js";
import { stepSixSchema } from "../step6/stepSixSchema.js";

describe("freelancer onboarding flow", () => {
  it("exports the onboarding manager component", () => {
    expect(OnboardingManager).toBeTypeOf("function");
  });

  it("validates each onboarding step schema", () => {
    expect(stepOneSchema.safeParse({
      tagline: "I build polished web products for small businesses.",
      bio: "I design and ship clean digital experiences for founders, agencies, and teams who need reliable execution.",
      phone: "+94771234567",
      experienceLevel: "intermediate",
    }).success).toBe(true);

    expect(stepTwoSchema.safeParse({
      categoryId: 5,
      skills: [{ skillId: 7, customName: "" }],
    }).success).toBe(true);

    expect(stepThreeSchema.safeParse({
      packages: [{
        packageType: "Starter",
        price: 149,
        deliveryDays: 5,
        description: "Landing page design and front-end polish.",
      }],
    }).success).toBe(true);

    expect(stepFourSchema.safeParse({
      portfolio: [{
        title: "Moodboard app",
        description: "Case study for a product launch landing page.",
        imageUrl: "https://example.com/portfolio.png",
      }],
    }).success).toBe(true);

    expect(stepFiveSchema.safeParse({ confirmed: true }).success).toBe(true);
    expect(stepSixSchema.safeParse({ paypalEmail: "freelancer@example.com" }).success).toBe(true);
  });
});
