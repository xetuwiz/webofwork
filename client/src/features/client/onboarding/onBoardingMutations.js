import { useMutation, useQueryClient } from "@tanstack/react-query";
import { sendPhoneOtp, submitFreelancerOnboarding, verifyPhoneOtp } from "./OnboardingAPI.js";
import { createPortfolioItem, updatePaymentSettings } from "./OnboardingAPI.js";

export const useSendPhoneOtp = () => useMutation({ mutationFn: sendPhoneOtp });
export const useVerifyPhoneOtp = () => useMutation({ mutationFn: verifyPhoneOtp });
export const useSubmitFreelancerOnboarding = () => {
  const client = useQueryClient();
  return useMutation({ mutationFn: submitFreelancerOnboarding, onSuccess: () => client.invalidateQueries({ queryKey: ["freelancer-profile"] }) });
};
export const useCreatePortfolioItem = () => {
  const client = useQueryClient();
  return useMutation({ mutationFn: createPortfolioItem, onSuccess: () => client.invalidateQueries({ queryKey: ["freelancer-profile"] }) });
};
export const useUpdateOnboardingPaymentSettings = () => {
  const client = useQueryClient();
  return useMutation({ mutationFn: updatePaymentSettings, onSuccess: () => client.invalidateQueries({ queryKey: ["payment-settings"] }) });
};
