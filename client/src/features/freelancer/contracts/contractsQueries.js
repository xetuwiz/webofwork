import { useQuery, keepPreviousData } from "@tanstack/react-query";
import { fetchAllContracts, fetchContractDetail } from "./contractsApi";

export const useMyContracts = ({ page = 1, page_size = 20, status = "", role = "", search = "" } = {}) => {
  return useQuery({
    // Every filter is part of the key, including the empty ones. A partial key
    // means two different filter states collide on one cached response.
    queryKey: ["contracts", { page, page_size, status, role, search }],
    queryFn: () => fetchAllContracts({ page, page_size, status, role, search }),
    // `keepPreviousData: true` was the react-query v4 spelling and was removed in
    // v5, so it was silently ignored. The v5 equivalent keeps the current rows on
    // screen while the next page loads instead of blanking the list.
    placeholderData: keepPreviousData,
  });
};

export const useContractDetail = (id) => {
  return useQuery({
    queryKey: ["contract", id],
    queryFn: () => fetchContractDetail(id),
    enabled: !!id, 
  });
};