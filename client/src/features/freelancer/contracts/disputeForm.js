import { z } from "zod";

export const disputeFormSchema = z.object({
  reason: z.string().trim().min(1, "A dispute reason is required."),
  description: z.string().trim().min(10, "Please provide at least 10 characters of detail."),
});

export function validateDisputeForm(values) {
  const result = disputeFormSchema.safeParse(values);
  return {
    valid: result.success,
    errors: result.success ? {} : Object.fromEntries(result.error.issues.map((issue) => [issue.path[0], issue.message])),
  };
}

export function toDisputePayload(contractId, values) {
  const result = validateDisputeForm(values);
  if (!result.valid) throw new Error("Invalid dispute form");
  return { contractId, reason: values.reason.trim(), description: values.description.trim(), evidenceUrls: [] };
}
