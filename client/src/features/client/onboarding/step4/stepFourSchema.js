import { z } from "zod";
export const stepFourSchema = z.object({ portfolio: z.array(z.object({ title: z.string().min(1), description: z.string().min(1), imageUrl: z.string().url() })).optional() });
