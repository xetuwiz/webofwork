import axiosInstance from "../../../api/axiosInstance.js";

export const fetchAllContracts = async (params = {}) => (await axiosInstance.get("/contracts", { params })).data;
export const fetchContractDetail = async (id) => (await axiosInstance.get(`/contracts/${id}`)).data;
export const submitContractWork = async ({ id, payload }) => (await axiosInstance.post(`/deliverables/contract/${id}`, payload)).data;
export const postRevisionAction = async ({ revisionId, action, message }) => (await axiosInstance.post(`/revisions/${revisionId}/action`, { action, message })).data;
export const createDispute = async ({ contractId, reason, description, evidenceUrls = [] }) => (await axiosInstance.post(`/disputes/contract/${contractId}`, { reason, description, evidenceUrls })).data;
