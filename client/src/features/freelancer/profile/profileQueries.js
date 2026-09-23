import { useQuery } from "@tanstack/react-query";
import { fetchFreelancerProfile, fetchFreelancerTransactions, fetchPaymentSettings } from "./profileApi.js";
export const useFreelancerProfile = () => useQuery({ queryKey: ["freelancer-profile"], queryFn: fetchFreelancerProfile });
export const useFreelancerTransactions = (filters = {}) => useQuery({ queryKey: ["freelancer-transactions", filters], queryFn: () => fetchFreelancerTransactions(filters) });
export const usePaymentSettings = () => useQuery({ queryKey: ["payment-settings"], queryFn: fetchPaymentSettings });
