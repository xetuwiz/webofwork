import { useMutation, useQueryClient } from "@tanstack/react-query";
import { createPrivacyPolicy, deletePrivacyPolicy, updatePrivacyPolicy } from "./privacyApi";
import { toastApiError, toastSuccess } from "../../../utils/toast";

export const usePrivacyMutations = () => {
  const queryClient = useQueryClient();

  const createMutation = useMutation({
    mutationFn: createPrivacyPolicy,
    onSuccess: () => {
      queryClient.invalidateQueries(["privacypolicy"]);
      toastSuccess("Section added successfully");
    },
    onError: (error) => toastApiError(error, "Failed to add section"),
  });

  const updateMutation = useMutation({
    mutationFn: updatePrivacyPolicy,
    onSuccess: () => {
      queryClient.invalidateQueries(["privacypolicy"]);
      toastSuccess("Section updated successfully");
    },
    onError: (error) => toastApiError(error, "Failed to update section"),
  });

  const deleteMutation = useMutation({
    mutationFn: deletePrivacyPolicy,
    onSuccess: () => {
      queryClient.invalidateQueries(["privacypolicy"]);
      toastSuccess("Section deleted successfully");
    },
    onError: (error) => toastApiError(error, "Failed to delete section"),
  });

  return { createMutation, updateMutation, deleteMutation };
};