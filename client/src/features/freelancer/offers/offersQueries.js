import { useQuery, useMutation, useQueryClient, keepPreviousData } from "@tanstack/react-query";
import { acceptContract, fetchMyOffers } from "./offersApi";
import { toastError, toastSuccess } from "../../../utils/toast";
import { getErrorMessage } from "../../../utils/errorMessage";

export const useMyOffers = ({ page = 1, page_size = 20 } = {}) => {
  return useQuery({
    // This hook used to take no arguments at all, so FreelancerOffers.jsx's
    // `useMyOffers(page)` passed a page number that was silently discarded and
    // the key was the constant ["my-offers"]. Paging was therefore dead: React
    // Query served the one cached response forever.
    queryKey: ["my-offers", { page, page_size }],
    queryFn: () => fetchMyOffers({ page, page_size }),
    placeholderData: keepPreviousData,
  });
};



export const useAcceptOffer = () => {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: acceptContract,
    onSuccess: () => {
      // Prefix form: the key is now ["my-offers", { page, page_size }], so an
         // exact ["my-offers"] would not match any query.
        // Invalidation still matches the new ["my-offers", { page, page_size }]
        // key: react-query matches a queryKey as a prefix by default. Verified
        // against v5.90 rather than assumed, because silently breaking every
        // invalidation in the app would leave stale lists on screen. It does
        // match somewhat more broadly than the key suggests, which is the safe
        // direction -- it refetches too much rather than too little.
        queryClient.invalidateQueries({ queryKey: ["my-offers"] });
        queryClient.invalidateQueries(["contracts"]);
      toastSuccess("Contract signed successfully! Project started.");
    },
    onError: (error) => {
      const msg = getErrorMessage(error, "Failed to sign contract");
      toastError(msg);
    }
  });
};