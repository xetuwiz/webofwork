import axiosInstance from "../../../api/axiosInstance.js";
export const fetchSkills = async (params = {}) => (await axiosInstance.get("/categories/skills", { params })).data;
export const createSkill = async (data) => (await axiosInstance.post("/categories/skills", data)).data;
export const updateSkill = async (id, data) => (await axiosInstance.patch(`/categories/skills/${id}`, data)).data;
export const deleteSkill = async (id) => (await axiosInstance.delete(`/categories/skills/${id}`)).data;
export const fetchAllCategories = async () => (await axiosInstance.get("/categories")).data;
export const createCategory = async (data) => (await axiosInstance.post("/categories/create", data)).data;
export const updateCategory = async (id, data) => (await axiosInstance.patch(`/categories/${id}`, data)).data;
export const deleteCategory = async (id) => (await axiosInstance.delete(`/categories/${id}`)).data;
