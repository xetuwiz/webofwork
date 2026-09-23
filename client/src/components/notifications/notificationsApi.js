import axiosInstance from "../../api/axiosInstance.js";
export const fetchNotifications = async (params = {}) => (await axiosInstance.get("/notifications", { params })).data;
export const markNotificationRead = async (id) => (await axiosInstance.patch(`/notifications/${id}/read`)).data;
export const markAllNotificationsRead = async () => (await axiosInstance.patch("/notifications/read-all")).data;
export const deleteNotification = async (id) => (await axiosInstance.delete(`/notifications/${id}`)).data;
