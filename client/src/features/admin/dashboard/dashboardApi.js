import axiosInstance from "../../../api/axiosInstance.js";
export const fetchDashboardStats = async () => (await axiosInstance.get("/admin/dashboard-stats")).data;
export const fetchRevenueChart = async (params = {}) => (await axiosInstance.get("/admin/revenue-chart", { params })).data;
export const fetchProposalsChart = async (params = {}) => (await axiosInstance.get("/admin/proposals-chart", { params })).data;
