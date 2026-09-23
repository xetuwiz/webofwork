import { useMutation, useQueryClient } from "@tanstack/react-query";
import { createFAQ, deleteFAQ, updateFAQ } from "./faqApi.js";
export const useFAQMutations = () => { const client = useQueryClient(); const refresh = () => client.invalidateQueries({ queryKey: ["admin-faqs"] }); return { create: useMutation({ mutationFn: createFAQ, onSuccess: refresh }), update: useMutation({ mutationFn: updateFAQ, onSuccess: refresh }), remove: useMutation({ mutationFn: deleteFAQ, onSuccess: refresh }) }; };
export const useCreateFAQ = () => { const client = useQueryClient(); return useMutation({ mutationFn: createFAQ, onSuccess: () => client.invalidateQueries({ queryKey: ["admin-faqs"] }) }); };
export const useUpdateFAQ = () => { const client = useQueryClient(); return useMutation({ mutationFn: updateFAQ, onSuccess: () => client.invalidateQueries({ queryKey: ["admin-faqs"] }) }); };
export const useDeleteFAQ = () => { const client = useQueryClient(); return useMutation({ mutationFn: deleteFAQ, onSuccess: () => client.invalidateQueries({ queryKey: ["admin-faqs"] }) }); };
