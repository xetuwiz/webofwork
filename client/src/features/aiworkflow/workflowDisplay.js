export const workflowRows = (data) => data?.results || data?.items || (Array.isArray(data) ? data : []);
export const workflowState = (workflow) => ({ status: workflow?.status ?? "Unknown", canApprove: ["PendingApproval", "ValidationFailed"].includes(String(workflow?.status)), hasSteps: Boolean(workflow?.stepLogs || workflow?.steps) });
