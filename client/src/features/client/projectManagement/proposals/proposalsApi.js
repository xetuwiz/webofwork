import axiosInstance from "../../../../api/axiosInstance.js";

const normalizePage = (data) => Array.isArray(data) ? { results: data, count: data.length, page: 1, pageSize: data.length, totalPages: 1 } : data;

export const getSentInvitations = async (projectId) => normalizePage((await axiosInstance.get("/invitations/sent", { params: { projectId } })).data);
export const getProposals = async (projectId, params = {}) => normalizePage((await axiosInstance.get(`/proposals/project/${projectId}`, { params })).data);
export const rejectProposal = async (proposalId) => (await axiosInstance.patch(`/proposals/${proposalId}/reject`)).data;
