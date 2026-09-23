import { useMutation, useQueryClient } from "@tanstack/react-query";
import { resolveDispute } from "./disputeApi.js";
export const useResolveDispute = () => { const client = useQueryClient(); return useMutation({ mutationFn: resolveDispute, onSuccess: () => { client.invalidateQueries({ queryKey: ["admin-disputes"] }); client.invalidateQueries({ queryKey: ["admin-dispute"] }); } }); };
