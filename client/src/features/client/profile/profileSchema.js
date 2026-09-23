import { z } from "zod";

export const profileSchema = z.object({
  fullName: z.string().trim().min(3, "Full name is too short").max(200),
  companyName: z.string().trim().max(200).optional(),
  location: z.string().trim().max(200).optional(),
});
