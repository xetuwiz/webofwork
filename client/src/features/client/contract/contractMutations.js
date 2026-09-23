import { useMutation, useQueryClient } from "@tanstack/react-query";
import { acceptContract, createReview, requestContractRevision, updateContractStatus, verifyPayment } from "./contractApi.js";

const refreshContracts = (client) => {
  client.invalidateQueries({ queryKey: ["contracts"] });
  client.invalidateQueries({ queryKey: ["contract"] });
};

export const useVerifyPayment = () => {
  const client = useQueryClient();
  return useMutation({ mutationFn: verifyPayment, onSuccess: () => refreshContracts(client) });
};
export const useRequestRevision = () => {
  const client = useQueryClient();
  return useMutation({ mutationFn: requestContractRevision, onSuccess: () => refreshContracts(client) });
};
export const useUpdateContractStatus = () => {
  const client = useQueryClient();
  return useMutation({ mutationFn: updateContractStatus, onSuccess: () => refreshContracts(client) });
};
export const useAcceptContract = () => {
  const client = useQueryClient();
  return useMutation({ mutationFn: acceptContract, onSuccess: () => refreshContracts(client) });
};
export const useCreateReview = () => {
  const client = useQueryClient();
  return useMutation({ mutationFn: createReview, onSuccess: () => refreshContracts(client) });
};
