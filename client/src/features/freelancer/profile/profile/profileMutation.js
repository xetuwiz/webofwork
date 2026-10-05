import { useMutation, useQueryClient } from "@tanstack/react-query";
import { updatePaymentSettings, updateProfile } from "./profileApi";
import { toastApiError, toastError, toastSuccess } from "../../../utils/toast";

export const useUpdateFreelancerProfile = () => {
    const queryClient = useQueryClient();

    return useMutation({
        mutationFn: updateProfile,
        onSuccess: (updatedData) => {
            queryClient.invalidateQueries({ queryKey: ['freelancerProfile'] });
            
            queryClient.setQueryData(['freelancerProfile'], updatedData);
            
            toastSuccess("Profile updated successfully!");
        },
        onError: (error) => {
            // Walks the same shapes in the same order of specificity as before --
            // validation `errors` dictionary, then `title`, `detail`, `error`,
            // `message`, then the fallback -- but via the shared helper, so the
            // precedence lives in one place. The previous comment here warned
            // that reading only `.detail` made every model-binding and validation
            // failure show a bare "Failed to update profile".
            toastApiError(error, "Failed to update profile");
        }
    });
};



export const useUpdatePaymentSettings = () => {
    const queryClient = useQueryClient();
    return useMutation({
        mutationFn: updatePaymentSettings,
        onSuccess: () => {
            queryClient.invalidateQueries(["paymentSettings"]);
            toastSuccess("PayPal email updated successfully");
        },
        onError: () => toastError("Failed to update settings")
    });
};