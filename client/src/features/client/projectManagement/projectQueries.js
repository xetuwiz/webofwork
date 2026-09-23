import { useQuery } from "@tanstack/react-query";
import { fetchAllCategories, fetchAllSkills, fetchCategories, fetchMyProjects, fetchProjectById } from "./ProjectApi.js";

export const useCategories = () => useQuery({ queryKey: ["categories"], queryFn: fetchCategories });
export const useSkills = (params = {}) => useQuery({ queryKey: ["skills", params], queryFn: () => fetchAllSkills(params) });
export const useAllSkillls = () => useQuery({ queryKey: ["skills", "all"], queryFn: fetchAllSkills });
export const useAllSkills = useAllSkillls;
export const useAllCategories = () => useQuery({ queryKey: ["categories", "all"], queryFn: fetchAllCategories });
export const useMyProjects = (params = {}) => useQuery({ queryKey: ["projects", params], queryFn: fetchMyProjects });
export const useProjectDetails = (id) => useQuery({ queryKey: ["project", id], queryFn: () => fetchProjectById(id), enabled: Boolean(id) });
