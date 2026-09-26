import { useMutation, useQueryClient } from "@tanstack/react-query";
import { rejectProposal } from "./proposalsApi";
import { toastApiError, toastSuccess } from "../../../../utils/toast";

export const useRejectProposal = () => {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (proposalId) => rejectProposal(proposalId),
    onSuccess: (_, proposalId) => {
      queryClient.invalidateQueries(["proposals"]);
      toastSuccess("Proposal rejected successfully");
    },
    // See `payoutMutations.js` for why the shared helper is used instead of a
    // hand-rolled `data?.error` read.
    onError: (error) => toastApiError(error, "Failed to reject proposal"),
  });
};