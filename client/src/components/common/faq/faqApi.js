import axiosInstance from "../../../api/axiosInstance.js";
export const fetchFAQs = async (params = {}) => (await axiosInstance.get("/content/faq", { params })).data;
