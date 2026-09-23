import { useQuery } from "@tanstack/react-query";
import { fetchRecommendedProjects, fetchTodos } from "./DashboardApi.js";
export const useRecommendedProjects = () => useQuery({ queryKey: ["recommended-projects"], queryFn: fetchRecommendedProjects });
export const useTodos = () => useQuery({ queryKey: ["todos"], queryFn: fetchTodos });
