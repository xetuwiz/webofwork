import { useMutation, useQueryClient } from "@tanstack/react-query";
import { rejectProposal } from "./proposalsApi.js";

export const useRejectProposal = () => {
  const client = useQueryClient();
  return useMutation({ mutationFn: rejectProposal, onSuccess: () => client.invalidateQueries({ queryKey: ["proposals"] }) });
};
