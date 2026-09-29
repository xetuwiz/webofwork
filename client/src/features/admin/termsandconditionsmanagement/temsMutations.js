import { useMutation, useQueryClient } from "@tanstack/react-query";
import { createTermsSection, deleteTermsSection, updateTermsSection } from "./termsApi";
import { toastApiError, toastSuccess } from "../../../utils/toast";

export const useTermsMutations = () => {
  const queryClient = useQueryClient();

  // `toastApiError` rather than a hand-rolled `data?.error` read, for the reason
  // documented in `payoutMutations.js`: several of these endpoints answer with
  // ASP.NET ProblemDetails, where the message is under `detail`, and reading only
  // `error` reduced every rejection to a bare fallback string.
  const createMutation = useMutation({
    mutationFn: createTermsSection,
    onSuccess: () => {
      queryClient.invalidateQueries(["termsSections"]);
      toastSuccess("Section added successfully");
    },
    onError: (error) => toastApiError(error, "Failed to add section"),
  });

  const updateMutation = useMutation({
    mutationFn: updateTermsSection,
    onSuccess: () => {
      queryClient.invalidateQueries(["termsSections"]);
      toastSuccess("Section updated successfully");
    },
    onError: (error) => toastApiError(error, "Failed to update section"),
  });

  const deleteMutation = useMutation({
    mutationFn: deleteTermsSection,
    onSuccess: () => {
      queryClient.invalidateQueries(["termsSections"]);
      toastSuccess("Section deleted successfully");
    },
    onError: (error) => toastApiError(error, "Failed to delete section"),
  });

  return { createMutation, updateMutation, deleteMutation };
};