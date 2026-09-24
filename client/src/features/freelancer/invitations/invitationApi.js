import axiosInstance from "../../../api/axiosInstance.js";
const normalizePage = (data) => Array.isArray(data) ? { results: data, count: data.length, page: 1, pageSize: data.length, totalPages: 1 } : data;
export const getReceivedInvitations = async (params = {}) => normalizePage((await axiosInstance.get("/invitations/received", { params })).data);
export const updateInvitationStatus = async ({ id, status }) => (await axiosInstance.patch(`/invitations/${id}/status`, { status })).data;
