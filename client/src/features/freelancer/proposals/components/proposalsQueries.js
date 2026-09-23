import { useQuery } from "@tanstack/react-query";
import { fetchPlatformFee, fetchProposals } from "./proposalApi.js";
export const useMyProposals = (params = {}) => useQuery({ queryKey: ["my-proposals", params], queryFn: () => fetchProposals(params) });
export const usePlatformFee = () => useQuery({ queryKey: ["platform-fee"], queryFn: fetchPlatformFee });
