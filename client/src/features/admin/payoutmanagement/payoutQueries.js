import { useQuery, keepPreviousData } from "@tanstack/react-query";
import { fetchPendingPayouts } from "./payoutApi";

export const usePendingPayouts = ({ page = 1, page_size = 20 } = {}) => {
  return useQuery({
    queryKey: ["pending-payouts", { page, page_size }],
    queryFn: () => fetchPendingPayouts({ page, page_size }),
    placeholderData: keepPreviousData,
    staleTime: 1000 * 60 * 5,
  });
};