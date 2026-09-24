import { useMutation, useQueryClient } from "@tanstack/react-query";
import { createProject, deleteProject, updateProject } from "./ProjectApi.js";
import { startAiWorkflow } from "../aiworkflow/aiWorkflowApi.js";

export const useCreateProject = () => {
  const client = useQueryClient();
  return useMutation({ mutationFn: createProject, onSuccess: () => client.invalidateQueries({ queryKey: ["projects"] }) });
};
export const useUpdateProject = () => {
  const client = useQueryClient();
  return useMutation({ mutationFn: updateProject, onSuccess: (_, variables) => { client.invalidateQueries({ queryKey: ["projects"] }); client.invalidateQueries({ queryKey: ["project", variables.id] }); } });
};
export const useDeleteProject = () => {
  const client = useQueryClient();
  return useMutation({ mutationFn: deleteProject, onSuccess: () => client.invalidateQueries({ queryKey: ["projects"] }) });
};
export const useStartAiWorkflow = () => {
  const client = useQueryClient();
  return useMutation({ mutationFn: startAiWorkflow, onSuccess: () => { client.invalidateQueries({ queryKey: ["ai-workflow"] }); client.invalidateQueries({ queryKey: ["project"] }); } });
};
