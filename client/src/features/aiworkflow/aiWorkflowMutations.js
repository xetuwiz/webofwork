import { useMutation, useQueryClient } from "@tanstack/react-query";
import { approveAiWorkflow, cancelAiWorkflow } from "./aiWorkflowApi.js";
export const useApproveAiWorkflow = () => { const client = useQueryClient(); return useMutation({ mutationFn: approveAiWorkflow, onSuccess: () => { client.invalidateQueries({ queryKey: ["ai-workflows"] }); client.invalidateQueries({ queryKey: ["ai-workflow"] }); } }); };
export const useCancelAiWorkflow = () => { const client = useQueryClient(); return useMutation({ mutationFn: cancelAiWorkflow, onSuccess: () => client.invalidateQueries({ queryKey: ["ai-workflows"] }) }); };
