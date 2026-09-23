import { useMutation, useQueryClient } from "@tanstack/react-query";
import { createProposal } from "./proposalApi.js";
export const useSubmitProposal = () => { const client = useQueryClient(); return useMutation({ mutationFn: createProposal, onSuccess: () => client.invalidateQueries({ queryKey: ["my-proposals"] }) }); };
