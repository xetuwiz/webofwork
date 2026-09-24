import axiosInstance from "../../../api/axiosInstance.js";
export const fetchAdminReviews = async (params = {}) => (await axiosInstance.get("/reviews/admin", { params })).data;
export const deleteReview = async (id) => (await axiosInstance.delete(`/reviews/${id}`)).data;
