import { useQuery } from "@tanstack/react-query";
import { fetchAllCategories, fetchSkills } from "./categoryApi.js";
export const useCategories = (params = {}) => useQuery({ queryKey: ["admin-categories", params], queryFn: () => fetchAllCategories(params) });
export const useAllCategories = () => useQuery({ queryKey: ["categories"], queryFn: fetchAllCategories });
export const useSkills = (params = {}) => useQuery({ queryKey: ["admin-skills", params], queryFn: () => fetchSkills(params) });
