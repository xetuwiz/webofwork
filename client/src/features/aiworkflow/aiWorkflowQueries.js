import { useQuery } from "@tanstack/react-query";
import { fetchAiWorkflow, fetchAiWorkflows, fetchAiWorkflowStatus } from "./aiWorkflowApi.js";
export const useAiWorkflows = (params = {}) => useQuery({ queryKey: ["ai-workflows", params], queryFn: () => fetchAiWorkflows(params) });
export const useAiWorkflow = (id) => useQuery({ queryKey: ["ai-workflow", id], queryFn: () => fetchAiWorkflow(id), enabled: Boolean(id) });
export const useAiWorkflowStatus = (projectId) => useQuery({ queryKey: ["ai-workflow", "project", projectId], queryFn: () => fetchAiWorkflowStatus(projectId), enabled: Boolean(projectId) });
