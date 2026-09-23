import axiosInstance from "../../../api/axiosInstance.js";
export const fetchSupportTickets = async (params = {}) => (await axiosInstance.get("/support/admin/tickets", { params })).data;
export const fetchTicketDetails = async (id) => (await axiosInstance.get(`/support/admin/tickets/${id}`)).data;
export const updateTicketReply = async ({ id, reply }) => (await axiosInstance.patch(`/support/admin/tickets/${id}/reply`, { reply })).data;
