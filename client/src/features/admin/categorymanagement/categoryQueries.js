import { useQuery, keepPreviousData } from "@tanstack/react-query";
import axiosInstance from "../../../api/axiosInstance";
import { fetchAllCategories } from "./categoryApi";

const fetchCategories = async ({ page = 1, page_size = 20, search = "" } = {}) => {
  // CategoriesController already accepts `page_size`, so this needed no server
  // change -- the client simply never sent it, so the page-size selector would
  // have had nothing to control.
  const res = await axiosInstance.get("/categories/", { params: { page, page_size, search } });
  return res.data;
};

export const useCategories = ({ page = 1, page_size = 20, search = "" } = {}) => {
  return useQuery({
    queryKey: ["admin-categories", { page, page_size, search }],
    queryFn: () => fetchCategories({ page, page_size, search }),
    // `keepPreviousData` was the react-query v4 spelling, removed in v5, so it
    // was silently ignored. This is the v5 equivalent.
    placeholderData: keepPreviousData,
  });
};


export const useAllCategories = () => {
  return useQuery({
    queryKey: ["all-categories-list"],
    queryFn: fetchAllCategories,
    staleTime: 1000 * 60 * 30, 
  });
};