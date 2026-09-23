import { useQuery } from "@tanstack/react-query";
import { fetchNotifications } from "./notificationsApi.js";
export const useNotifications = (params = {}) => useQuery({ queryKey: ["notifications", params], queryFn: () => fetchNotifications(params) });
