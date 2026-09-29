import { useQuery, keepPreviousData } from "@tanstack/react-query"
import { fetchFreelancerProfile, fetchFreelancerTransactions, fetchPaymentSettings } from "./profileApi"

export const useFreelancerProfile = ()=>{
    return useQuery({
        queryKey:['freelancerProfile'],
        queryFn:fetchFreelancerProfile,
        staleTime:1000 * 60 * 5
    })
}

export const useFreelancerTransactions = (filters) => {
  return useQuery({
    queryKey: ["freelancerTransactions", filters],
    queryFn: () => fetchFreelancerTransactions(filters),
    // `keepPreviousData: true` was the react-query v4 spelling and was removed in
    // v5, so it was silently ignored. The v5 equivalent keeps the current rows on
    // screen while the next page loads instead of blanking the list.
    placeholderData: keepPreviousData,
    staleTime: 1000 * 60 * 5,
  });
};


export const usePaymentSettings = () => {
    return useQuery({
        queryKey: ["paymentSettings"],
        queryFn: fetchPaymentSettings,
        staleTime:1000 * 60 * 5
    });
};