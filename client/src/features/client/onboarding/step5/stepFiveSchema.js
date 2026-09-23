import { z } from "zod";
export const stepFiveSchema = z.object({ confirmed: z.literal(true) });
