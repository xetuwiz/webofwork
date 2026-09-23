import { useQuery } from "@tanstack/react-query";
import { fetchTermsSections } from "./termsApi.js";
export const useTermsSections = (params = {}) => useQuery({ queryKey: ["admin-terms", params], queryFn: () => fetchTermsSections(params) });
