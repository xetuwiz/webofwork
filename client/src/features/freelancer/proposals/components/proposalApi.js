import axiosInstance from "../../../../api/axiosInstance.js";
export const createProposal = async (payload) => (await axiosInstance.post("/proposals", payload)).data;
export const fetchProposals = async (params = {}) => (await axiosInstance.get("/proposals/my", { params })).data;
export const fetchPlatformFee = async () => (await axiosInstance.get("/common/platform-percentage")).data;
