import { useQuery, keepPreviousData } from "@tanstack/react-query";
import { fetchPublicFAQs } from "./faqApi";

export const useFAQs = ({ category = "", page = 1, page_size = 20 } = {}) => {
  return useQuery({
    queryKey: ["public-faqs", { category, page, page_size }],
    queryFn: () => fetchPublicFAQs({ category, page, page_size }),
    staleTime: 1000 * 60 * 10,
    // `keepPreviousData: true` was the react-query v4 spelling and was removed in
    // v5, so it was silently ignored. The v5 equivalent keeps the current rows on
    // screen while the next page loads instead of blanking the list.
    placeholderData: keepPreviousData,
  });
};
