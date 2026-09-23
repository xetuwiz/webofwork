import { z } from "zod";
export const stepSixSchema = z.object({ paypalEmail: z.string().email() });
