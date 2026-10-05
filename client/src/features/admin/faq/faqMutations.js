import { useMutation, useQueryClient } from "@tanstack/react-query";
import { createFAQ, updateFAQ, deleteFAQ } from "./faqApi";
import { toastError, toastSuccess } from "../../../utils/toast";

export const useFAQMutations = () => {
    const queryClient = useQueryClient();

    const createMutation = useMutation({
        mutationFn: createFAQ,
        onSuccess: () => {
            queryClient.invalidateQueries(["admin-faqs"]);
            toastSuccess("FAQ created successfully");
        },
        onError: () => toastError("Failed to create FAQ")
    });

    const updateMutation = useMutation({
        mutationFn: updateFAQ,
        onSuccess: () => {
            queryClient.invalidateQueries(["admin-faqs"]);
            toastSuccess("FAQ updated successfully");
        },
        onError: () => toastError("Failed to update FAQ")
    });

    const deleteMutation = useMutation({
        mutationFn: deleteFAQ,
        onSuccess: () => {
            queryClient.invalidateQueries(["admin-faqs"]);
            toastSuccess("FAQ deleted successfully");
        },
        onError: () => toastError("Failed to delete FAQ")
    });

    return { createMutation, updateMutation, deleteMutation };
};