import { useMutation, useQueryClient } from "@tanstack/react-query";
import { createPrivacyPolicy, deletePrivacyPolicy, updatePrivacyPolicy } from "./privacyApi.js";
export const usePrivacyMutations = () => { const client = useQueryClient(); const refresh = () => client.invalidateQueries({ queryKey: ["admin-privacy"] }); return { create: useMutation({ mutationFn: createPrivacyPolicy, onSuccess: refresh }), update: useMutation({ mutationFn: updatePrivacyPolicy, onSuccess: refresh }), remove: useMutation({ mutationFn: deletePrivacyPolicy, onSuccess: refresh }) }; };
