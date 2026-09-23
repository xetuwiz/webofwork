import { useQuery } from "@tanstack/react-query";
import { fetchAllContracts, fetchContractDetail } from "./contractsApi.js";
export const useMyContracts = (filters = {}) => useQuery({ queryKey: ["freelancer-contracts", filters], queryFn: () => fetchAllContracts(filters) });
export const useContractDetail = (id) => useQuery({ queryKey: ["contract", id], queryFn: () => fetchContractDetail(id), enabled: Boolean(id) });
