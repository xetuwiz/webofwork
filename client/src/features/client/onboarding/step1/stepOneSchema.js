import { z } from "zod";
export const stepOneSchema = z.object({
	tagline: z.string().min(10).max(80),
	bio: z.string().min(50).max(500),
	phone: z.string().min(10),
	experienceLevel: z.enum(["beginner", "intermediate", "expert"]),
});
