import axiosInstance from "../../../api/axiosInstance.js";
export const fetchUsers = async (params = {}) => (await axiosInstance.get("/admin/users", { params })).data;
export const toggleUserActive = async (userId) => (await axiosInstance.patch(`/admin/users/${userId}/toggle-active`)).data;
export const softDeleteUser = async (userId) => (await axiosInstance.delete(`/admin/users/${userId}/soft-delete`)).data;
