import axiosInstance from "../../../api/axiosInstance.js";
export const fetchAdminFAQs = async (params = {}) => (await axiosInstance.get("/content/admin/faq", { params })).data;
export const createFAQ = async (payload) => (await axiosInstance.post("/content/admin/faq", payload)).data;
export const updateFAQ = async ({ id, ...payload }) => (await axiosInstance.put(`/content/admin/faq/${id}`, payload)).data;
export const deleteFAQ = async (id) => (await axiosInstance.delete(`/content/admin/faq/${id}`)).data;
