import axiosInstance from "../../../api/axiosInstance.js";
export const fetchTermsSections = async (params = {}) => (await axiosInstance.get("/content/admin/cms", { params: { ...params, category: "terms" } })).data;
export const createTermsSection = async (payload) => (await axiosInstance.post("/content/admin/cms", { ...payload, category: "terms" })).data;
export const updateTermsSection = async ({ id, ...payload }) => (await axiosInstance.put(`/content/admin/cms/${id}`, { ...payload, category: "terms" })).data;
export const deleteTermsSection = async (id) => (await axiosInstance.delete(`/content/admin/cms/${id}`)).data;
