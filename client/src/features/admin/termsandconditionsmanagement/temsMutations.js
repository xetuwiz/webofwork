import { useMutation, useQueryClient } from "@tanstack/react-query";
import { createTermsSection, deleteTermsSection, updateTermsSection } from "./termsApi.js";
export const useTermsMutations = () => { const client = useQueryClient(); const refresh = () => client.invalidateQueries({ queryKey: ["admin-terms"] }); return { create: useMutation({ mutationFn: createTermsSection, onSuccess: refresh }), update: useMutation({ mutationFn: updateTermsSection, onSuccess: refresh }), remove: useMutation({ mutationFn: deleteTermsSection, onSuccess: refresh }) }; };
