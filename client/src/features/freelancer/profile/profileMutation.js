import { useMutation, useQueryClient } from "@tanstack/react-query";
import { updatePaymentSettings, updateProfile } from "./profileApi.js";
export const useUpdateFreelancerProfile = () => { const client = useQueryClient(); return useMutation({ mutationFn: updateProfile, onSuccess: () => client.invalidateQueries({ queryKey: ["freelancer-profile"] }) }); };
export const useUpdatePaymentSettings = () => { const client = useQueryClient(); return useMutation({ mutationFn: updatePaymentSettings, onSuccess: () => client.invalidateQueries({ queryKey: ["payment-settings"] }) }); };
