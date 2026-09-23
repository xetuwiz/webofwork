import { useQuery } from "@tanstack/react-query";
import { fetchDashboardStats, fetchProposalsChart, fetchRevenueChart } from "./dashboardApi.js";
export const useDashboardStats = () => useQuery({ queryKey: ["dashboard-stats"], queryFn: fetchDashboardStats });
export const useRevenueChart = (params = {}) => useQuery({ queryKey: ["revenue-chart", params], queryFn: () => fetchRevenueChart(params) });
export const useProposalsChart = (params = {}) => useQuery({ queryKey: ["proposals-chart", params], queryFn: () => fetchProposalsChart(params) });
