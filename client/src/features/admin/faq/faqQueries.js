import { useQuery, keepPreviousData } from "@tanstack/react-query";
import { fetchAdminFAQs } from "./faqApi";

export const useAdminFAQs = ({ page = 1, page_size = 20 } = {}) => {
    return useQuery({
        queryKey: ["admin-faqs", { page, page_size }],
        queryFn: () => fetchAdminFAQs({ page, page_size }),
        // `keepPreviousData: true` was the react-query v4 spelling and was removed in
    // v5, so it was silently ignored. The v5 equivalent keeps the current rows on
    // screen while the next page loads instead of blanking the list.
    placeholderData: keepPreviousData,
        staleTime: 1000 * 60 * 5,
    });
};