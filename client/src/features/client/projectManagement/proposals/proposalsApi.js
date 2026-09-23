import axiosInstance from "../../../../api/axiosInstance.js";

export const getSentInvitations = async (projectId) => (await axiosInstance.get("/invitations/sent", { params: { projectId } })).data;
export const getProposals = async (projectId, params = {}) => (await axiosInstance.get(`/proposals/project/${projectId}`, { params })).data;
export const rejectProposal = async (proposalId) => (await axiosInstance.patch(`/proposals/${proposalId}/reject`)).data;
