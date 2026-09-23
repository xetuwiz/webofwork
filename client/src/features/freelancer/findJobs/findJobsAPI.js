import axiosInstance from "../../../api/axiosInstance.js";
export const fetchFreelancerProjects = async (params = {}) => (await axiosInstance.get("/projects", { params })).data;
