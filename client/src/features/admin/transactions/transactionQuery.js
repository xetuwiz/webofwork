import { useQuery, keepPreviousData } from "@tanstack/react-query"
import { fetchTransactions } from "./transactionsApi"

export const usePlatformTransactions = ({ page = 1, page_size = 20, status = '' } = {}) => {
    return useQuery({
        queryKey: ['platform-transactions', { page, page_size, status }],
        queryFn: () => fetchTransactions({ page, page_size, status }),
        // `keepPreviousData: true` was the react-query v4 spelling and was removed in
    // v5, so it was silently ignored. The v5 equivalent keeps the current rows on
    // screen while the next page loads instead of blanking the list.
    placeholderData: keepPreviousData,
        staleTime: 1000 * 60 * 5,
    })
}