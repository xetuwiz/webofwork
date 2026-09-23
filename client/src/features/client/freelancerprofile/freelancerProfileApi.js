import axiosInstance from "../../../api/axiosInstance.js";

export const fetchFreelancerProfile = async (id) => (await axiosInstance.get(`/freelancers/${id}`)).data;
export const fetchFreelancerReviews = async (id) => (await axiosInstance.get(`/reviews/freelancer/${id}`)).data;
