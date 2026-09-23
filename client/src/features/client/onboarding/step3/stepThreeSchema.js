import { z } from "zod";
const packageSchema = z.object({
	packageType: z.string().min(1),
	price: z.coerce.number().nonnegative(),
	deliveryDays: z.coerce.number().int().positive(),
	description: z.string().default(""),
});
export const stepThreeSchema = z.object({ packages: z.array(packageSchema).min(1) });
