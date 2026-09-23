import { useQuery } from "@tanstack/react-query";
import { fetchPendingPayouts } from "./payoutApi.js";
export const usePendingPayouts = (params = {}) => useQuery({ queryKey: ["pending-payouts", params], queryFn: () => fetchPendingPayouts(params) });
