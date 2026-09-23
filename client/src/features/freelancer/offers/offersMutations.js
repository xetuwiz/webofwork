import { useMutation, useQueryClient } from "@tanstack/react-query";
import { acceptContract } from "./offersApi.js";
export const useAcceptOffer = () => { const client = useQueryClient(); return useMutation({ mutationFn: acceptContract, onSuccess: () => { client.invalidateQueries({ queryKey: ["freelancer-offers"] }); client.invalidateQueries({ queryKey: ["freelancer-contracts"] }); } }); };
