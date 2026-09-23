import { useQuery } from "@tanstack/react-query";
import { fetchFreelancerProjects } from "./findJobsAPI.js";
export const useFreelancerProjects = (filters = {}) => useQuery({ queryKey: ["freelancer-projects", filters], queryFn: () => fetchFreelancerProjects(filters) });
