import axiosInstance from "../../../api/axiosInstance.js";

const normalizePage = (data) => Array.isArray(data) ? { results: data, count: data.length, page: 1, pageSize: data.length, totalPages: 1 } : data;

export const fetchFreelancerProjects = async (params = {}) => normalizePage((await axiosInstance.get("/projects", { params })).data);
