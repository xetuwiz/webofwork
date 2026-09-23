import { useMutation, useQueryClient } from "@tanstack/react-query";
import { softDeleteUser, toggleUserActive } from "./userManagementApi.js";
export const useToggleUserActive = () => { const client = useQueryClient(); return useMutation({ mutationFn: toggleUserActive, onSuccess: () => client.invalidateQueries({ queryKey: ["admin-users"] }) }); };
export const useSoftDeleteUser = () => { const client = useQueryClient(); return useMutation({ mutationFn: softDeleteUser, onSuccess: () => client.invalidateQueries({ queryKey: ["admin-users"] }) }); };
