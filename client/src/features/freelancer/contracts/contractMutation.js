import { useMutation, useQueryClient } from "@tanstack/react-query";
import { createDispute, postRevisionAction, submitContractWork } from "./contractsApi.js";
const useRefresh = () => { const client = useQueryClient(); return () => { client.invalidateQueries({ queryKey: ["contract"] }); client.invalidateQueries({ queryKey: ["freelancer-contracts"] }); }; };
export const useSubmitWork = () => { const refresh = useRefresh(); return useMutation({ mutationFn: submitContractWork, onSuccess: refresh }); };
export const useRevisionAction = () => { const refresh = useRefresh(); return useMutation({ mutationFn: postRevisionAction, onSuccess: refresh }); };
export const useDisputeMutation = () => { const refresh = useRefresh(); return useMutation({ mutationFn: createDispute, onSuccess: refresh }); };
