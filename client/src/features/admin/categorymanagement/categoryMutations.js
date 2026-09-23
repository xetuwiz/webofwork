import { useMutation, useQueryClient } from "@tanstack/react-query";
import { createCategory, createSkill, deleteCategory, deleteSkill, updateCategory, updateSkill } from "./categoryApi.js";
const useRefresh = () => { const client = useQueryClient(); return () => { client.invalidateQueries({ queryKey: ["categories"] }); client.invalidateQueries({ queryKey: ["admin-categories"] }); client.invalidateQueries({ queryKey: ["admin-skills"] }); }; };
export const useCreateCategory = () => { const refresh = useRefresh(); return useMutation({ mutationFn: createCategory, onSuccess: refresh }); };
export const useUpdateCategory = () => { const refresh = useRefresh(); return useMutation({ mutationFn: ({ id, data }) => updateCategory(id, data), onSuccess: refresh }); };
export const useDeleteCategory = () => { const refresh = useRefresh(); return useMutation({ mutationFn: deleteCategory, onSuccess: refresh }); };
export const useCreateSkill = () => { const refresh = useRefresh(); return useMutation({ mutationFn: createSkill, onSuccess: refresh }); };
export const useUpdateSkill = () => { const refresh = useRefresh(); return useMutation({ mutationFn: ({ id, data }) => updateSkill(id, data), onSuccess: refresh }); };
export const useDeleteSkill = () => { const refresh = useRefresh(); return useMutation({ mutationFn: deleteSkill, onSuccess: refresh }); };
