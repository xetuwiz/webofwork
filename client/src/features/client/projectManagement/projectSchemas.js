import { z } from "zod";

export const projectCreateSchema = z.object({
  title: z.string().min(3),
  description: z.string().min(10),
  requirements: z.string().min(1),
  expectedDeliverables: z.string().min(1),
  pricingType: z.coerce.number().int().min(1).max(2),
  fixedPrice: z.coerce.number().nonnegative().optional(),
  minPrice: z.coerce.number().nonnegative().optional(),
  maxPrice: z.coerce.number().nonnegative().optional(),
  expectedDays: z.coerce.number().int().positive(),
  categoryId: z.coerce.number().int().positive().nullable().optional(),
  skillIds: z.array(z.coerce.number().int().positive()).default([]),
  customSkillNames: z.array(z.string().min(1)).default([]),
});
