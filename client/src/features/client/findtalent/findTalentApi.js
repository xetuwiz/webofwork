import axiosInstance from "../../../api/axiosInstance.js";

export const getFreelancerList = async (params = {}) => (await axiosInstance.get("/freelancers", { params })).data;
export const getFreelancerDetails = async (id) => (await axiosInstance.get(`/freelancers/${id}`)).data;
export const fetchEligibleJobs = async () => (await axiosInstance.get("/invitations/eligible-projects")).data;
export const createInvitation = async (payload) => (await axiosInstance.post("/invitations", payload)).data;
