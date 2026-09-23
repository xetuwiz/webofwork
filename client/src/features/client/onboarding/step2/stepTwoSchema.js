import { z } from "zod";
const skillSchema = z.object({ skillId: z.coerce.number().int().positive().nullable().optional(), customName: z.string().default("") }).refine((skill) => skill.skillId || skill.customName.trim().length > 0, "Select a skill or enter a custom skill");
export const stepTwoSchema = z.object({
	categoryId: z.coerce.number().int().positive(),
	skills: z.array(skillSchema).min(1),
});
