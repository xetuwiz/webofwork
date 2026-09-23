import { useQuery } from "@tanstack/react-query";
import { fetchTransactions } from "./transactionsApi.js";
export const usePlatformTransactions = (params = {}) => useQuery({ queryKey: ["admin-transactions", params], queryFn: () => fetchTransactions(params) });
