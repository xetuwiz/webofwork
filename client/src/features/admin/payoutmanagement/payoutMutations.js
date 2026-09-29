import { useMutation, useQueryClient } from '@tanstack/react-query';
import { refundPayment, releasePayout } from './payoutApi';
import { toastApiError, toastSuccess } from "../../../utils/toast";

export const useReleasePayout = () => {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (contractId) => releasePayout(contractId),
    onSuccess: () => {
      toastSuccess("Funds released successfully!");
      queryClient.invalidateQueries(['contracts']);
    },
    // `toastApiError` rather than a hand-rolled `data?.error` read.
    //
    // This used to show a bare "Payout failed" for every rejection, which matters
    // more now than it did: releasing a payout rejects for specific, actionable
    // reasons -- "Contract is not completed.", "Payout is not yet eligible.",
    // "Payout already released." -- and those arrive as ASP.NET ProblemDetails
    // under `detail`, not under the `error` key this was reading. The admin was
    // told the payout failed and nothing else, for failures that are usually just
    // a timing issue.
    onError: (error) => toastApiError(error, "Payout failed")
  });
};


export const useRefundPayment = () => {
    const queryClient = useQueryClient();
    return useMutation({
        mutationFn: refundPayment,
        onSuccess: () => {
            queryClient.invalidateQueries(['pending-payouts']);
            toastSuccess("Refund processed successfully");
        },
        onError: (error) => toastApiError(error, "Refund failed")
    });
};