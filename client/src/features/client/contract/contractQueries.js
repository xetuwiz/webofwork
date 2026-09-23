import { useQuery } from "@tanstack/react-query";
import { fetchContract, fetchContractOffers, fetchContracts } from "./contractApi.js";

export const useContracts = (options = {}) => useQuery({ queryKey: ["contracts", options], queryFn: fetchContracts });
export const useContract = (id) => useQuery({ queryKey: ["contract", id], queryFn: () => fetchContract(id), enabled: Boolean(id) });
export const useContractOffers = () => useQuery({ queryKey: ["contract-offers"], queryFn: fetchContractOffers });
