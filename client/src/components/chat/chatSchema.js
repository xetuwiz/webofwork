import { z } from "zod";
export const chatMessageSchema = z.object({ content: z.string().trim().min(1).max(4000) });
export const chatRoomSchema = z.object({ contractId: z.coerce.number().int().positive() });
