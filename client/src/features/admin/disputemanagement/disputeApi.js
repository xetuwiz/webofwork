import axiosInstance from "../../../api/axiosInstance.js";
export const fetchAdminDisputes = async (params = {}) => (await axiosInstance.get("/disputes", { params })).data;
export const fetchAdminDisputeDetail = async (id) => (await axiosInstance.get(`/disputes/${id}`)).data;
export const resolveDispute = async ({ id, status, adminNotes, resolution }) => (await axiosInstance.post(`/disputes/${id}/resolve`, { status, adminNotes, resolution })).data;
