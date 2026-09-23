import { useMutation, useQueryClient } from "@tanstack/react-query";
import { deleteNotification, markAllNotificationsRead, markNotificationRead } from "./notificationsApi.js";
const refresh = (client) => client.invalidateQueries({ queryKey: ["notifications"] });
export const useMarkNotificationRead = () => { const client = useQueryClient(); return useMutation({ mutationFn: markNotificationRead, onSuccess: () => refresh(client) }); };
export const useMarkAllNotificationsRead = () => { const client = useQueryClient(); return useMutation({ mutationFn: markAllNotificationsRead, onSuccess: () => refresh(client) }); };
export const useDeleteNotification = () => { const client = useQueryClient(); return useMutation({ mutationFn: deleteNotification, onSuccess: () => refresh(client) }); };
