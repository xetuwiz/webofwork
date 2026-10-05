import { useMutation, useQueryClient } from "@tanstack/react-query";
import { toastApiError, toastSuccess } from "../../../utils/toast";
import {
  createDispute,
  postRevisionAction,
  submitContractWork,
  updateDeliverable,
  deleteDeliverable,
} from "./contractsApi";

export const useSubmitWork = () => {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: submitContractWork,
    onSuccess: (data, variables) => {
      queryClient.invalidateQueries({ queryKey: ["contract", variables.id] });
    },
    onError: (error) => toastApiError(error, "Failed to submit work"),
  });
};

export const useRevisionAction = () => {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: postRevisionAction,
    onSuccess: () => {
      queryClient.invalidateQueries(["contractDetail"]);
    },
    // This used to `console.error` and nothing else, so accepting or disputing a
    // revision failed silently: the button did nothing, the contract stayed where
    // it was, and the only trace was in the browser console. It now tells the user,
    // and keeps the console line for diagnosis.
    onError: (error) => {
      toastApiError(error, "Failed to update revision status");
      console.error("Revision Action Error:", error);
    },
  });
};

export const useDisputeMutation = () => {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: createDispute,
    onSuccess: () => {
      queryClient.invalidateQueries(["contractDetail"]);
      toastSuccess("Dispute raised successfully!");
    },
    onError: (error) => toastApiError(error, "Failed to raise dispute"),
  });
};

export const useUpdateDeliverable = () => {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: updateDeliverable,
    onSuccess: (_data, variables) => {
      queryClient.invalidateQueries({ queryKey: ["contract", variables.id] });
      toastSuccess("Deliverable updated");
    },
    // 409 means the contract moved past review, so the edit is refused -- the most
    // likely failure here, and one worth explaining rather than reporting as a
    // generic failure.
    onError: (err) => toastApiError(err, "Failed to update deliverable"),
  });
};

export const useDeleteDeliverable = () => {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: deleteDeliverable,
    onSuccess: (_data, id) => {
      queryClient.invalidateQueries({ queryKey: ["contract"] });
      toastSuccess("Deliverable deleted");
    },
    onError: (err) => toastApiError(err, "Failed to delete deliverable"),
  });
};