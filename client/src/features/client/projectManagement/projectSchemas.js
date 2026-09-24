import { z } from "zod";

export const projectCreateSchema = z
  .object({
    title: z.string().min(5, "Project title must be at least 5 characters."),
    description: z.string().min(20, "Description must be at least 20 characters."),
    requirements: z.string().min(1, "Requirements are required."),
    expectedDeliverables: z.string().min(1, "Expected deliverables are required."),
    pricingType: z.coerce.number().int().min(1).max(2),
    fixedPrice: z.coerce.number().optional(),
    minPrice: z.coerce.number().optional(),
    maxPrice: z.coerce.number().optional(),
    expectedDays: z.coerce.number().int().positive("Expected days must be greater than zero."),
    categoryId: z.coerce.number().int().positive().nullable().optional(),
    skillIds: z.array(z.coerce.number().int().positive()).default([]),
    customSkillNames: z.array(z.string().min(1)).default([]),
  })
  .superRefine((data, ctx) => {
    if (data.pricingType === 1) {
      if (!data.fixedPrice || Number(data.fixedPrice) <= 0) {
        ctx.addIssue({
          code: z.ZodIssueCode.custom,
          path: ["fixedPrice"],
          message: "Fixed price is required and must be greater than 0 for fixed pricing.",
        });
      }
    }

    if (data.pricingType === 2) {
      if (!data.minPrice || Number(data.minPrice) <= 0) {
        ctx.addIssue({
          code: z.ZodIssueCode.custom,
          path: ["minPrice"],
          message: "Min price is required and must be greater than 0 for range pricing.",
        });
      }

      if (!data.maxPrice || Number(data.maxPrice) <= Number(data.minPrice ?? 0)) {
        ctx.addIssue({
          code: z.ZodIssueCode.custom,
          path: ["maxPrice"],
          message: "Max price is required and must be greater than min price for range pricing.",
        });
      }
    }
  });
