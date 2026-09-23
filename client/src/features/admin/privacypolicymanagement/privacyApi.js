import axiosInstance from "../../../api/axiosInstance.js";
export const fetchprivacypolicies = async (params = {}) => (await axiosInstance.get("/content/admin/cms", { params: { ...params, category: "privacy" } })).data;
export const createPrivacyPolicy = async (payload) => (await axiosInstance.post("/content/admin/cms", { ...payload, category: "privacy" })).data;
export const updatePrivacyPolicy = async ({ id, ...payload }) => (await axiosInstance.put(`/content/admin/cms/${id}`, { ...payload, category: "privacy" })).data;
export const deletePrivacyPolicy = async (id) => (await axiosInstance.delete(`/content/admin/cms/${id}`)).data;
