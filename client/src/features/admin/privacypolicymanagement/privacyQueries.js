import { useQuery } from "@tanstack/react-query";
import { fetchprivacypolicies } from "./privacyApi.js";
export const usePrivacyPolicy = (params = {}) => useQuery({ queryKey: ["admin-privacy", params], queryFn: () => fetchprivacypolicies(params) });
