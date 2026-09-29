import { useQuery, keepPreviousData } from "@tanstack/react-query";
import { fetchprivacypolicies } from "./privacyApi";

export const usePrivacyPolicy = ({ page = 1, page_size = 20 } = {}) => {
  return useQuery({
    queryKey: ["privacypolicy", { page, page_size }],
    queryFn: () => fetchprivacypolicies({ page, page_size }),
    // `keepPreviousData: true` was the react-query v4 spelling and was removed in
    // v5, so it was silently ignored. The v5 equivalent keeps the current rows on
    // screen while the next page loads instead of blanking the list.
    placeholderData: keepPreviousData,
  });
};