import axiosInstance from "../../api/axiosInstance.js";
export const createSupportTicket = async (payload) => (await axiosInstance.post("/support/tickets", payload)).data;
export const fetchMySupportTickets = async (params = {}) => (await axiosInstance.get("/support/tickets", { params })).data;
export const cancelSupportTicket = async (id) => (await axiosInstance.patch(`/support/tickets/${id}/cancel`)).data;
