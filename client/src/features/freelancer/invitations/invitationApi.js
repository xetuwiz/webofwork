import axiosInstance from "../../../api/axiosInstance.js";
export const getReceivedInvitations = async () => (await axiosInstance.get("/invitations/received")).data;
export const updateInvitationStatus = async ({ id, status }) => (await axiosInstance.patch(`/invitations/${id}/status`, { status })).data;
