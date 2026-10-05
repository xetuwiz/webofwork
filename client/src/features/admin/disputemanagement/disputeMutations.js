import { useMutation, useQueryClient } from "@tanstack/react-query";
import { resolveDispute } from "./disputeApi";
import { getErrorMessage } from "../../../utils/errorMessage";

export const useResolveDispute = () => {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: resolveDispute,
    onSuccess: (data, variables) => {
      queryClient.invalidateQueries(["admin", "disputes", variables.id]);
      queryClient.invalidateQueries(["admin", "disputes"]);
          },
    onError: (error) => {
      // `getErrorMessage` rather than `data?.error`: dispute resolution rejects
      // for real, specific reasons ("Only the parties to this dispute can resolve
      // it.", "A refund is already in progress"), and those arrive as
      // ProblemDetails under `detail`. This read only `error`, so the admin saw
      // the same generic line for every refusal.
      const message = getErrorMessage(error, "Failed to update case.");
      alert(`Decision Error: ${message}`);
    }
  });
};