import axiosInstance from "../../../api/axiosInstance.js";

export const fetchCategories = async () => (await axiosInstance.get("/categories")).data;
export const fetchSkills = async (params = {}) => (await axiosInstance.get("/categories/skills", { params })).data;
export const fetchAllSkills = fetchSkills;
export const fetchAllCategories = fetchCategories;
export const createProject = async (payload) => (await axiosInstance.post("/projects", payload)).data;
export const fetchMyProjects = async ({ queryKey }) => {
  const [, params = {}] = queryKey;
  return (await axiosInstance.get("/projects/my", { params })).data;
};
export const fetchProjectById = async (id) => (await axiosInstance.get(`/projects/${id}`)).data;
export const updateProject = async ({ id, data }) => (await axiosInstance.put(`/projects/${id}`, data)).data;
export const deleteProject = async ({ id }) => (await axiosInstance.delete(`/projects/${id}`)).data;
export const fetchRecommendedProjects = async () => (await axiosInstance.get("/projects/recommended")).data;
