import axiosInstance from "../../api/axiosInstance";

// Single source for the notification list. This used to call
// /communication/notifications/, a duplicate route that returned a `results`
// envelope and a slimmer item shape. The server now applies that route's
// TargetRole filtering inside /api/notifications/, so both clients share one
// endpoint and one envelope: { data: { unread_count, total_count, items } }.
export const fetchNotifications = async (fetchAll = false) => {
  const { data } = await axiosInstance.get(`/notifications/?all=${fetchAll}`);
  return data;
};

export const markAsRead = async (id) => {
  const { data } = await axiosInstance.patch(`/notifications/${id}/read/`);
  return data;
};
