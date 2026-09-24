import axiosInstance from "../../api/axiosInstance.js";
export const startAiWorkflow = async (payload) => (await axiosInstance.post("/ai/workflows/start", payload)).data;
export const fetchAiWorkflows = async (params = {}) => (await axiosInstance.get("/ai/workflows", { params })).data;
export const fetchAiWorkflowStatus = async (projectId) => (await axiosInstance.get(`/ai/workflows/by-project/${projectId}`)).data;
export const fetchAiWorkflow = async (id) => (await axiosInstance.get(`/ai/workflows/${id}`)).data;
export const approveAiWorkflow = async ({ id, decision, rationale, contractSpec }) => (await axiosInstance.post(`/ai/workflows/${id}/approve`, { decision, rationale, ...(contractSpec || {}) })).data;
export const cancelAiWorkflow = async (id) => (await axiosInstance.post(`/ai/workflows/${id}/cancel`)).data;
