// features/admin/categories/categoryMutations.js
import { useMutation, useQueryClient } from "@tanstack/react-query";
import axiosInstance from "../../../api/axiosInstance";

export const useCreateCategory = () => {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (payload) =>
      axiosInstance.post("/categories/create/", payload),
    onSuccess: () => {
      queryClient.invalidateQueries(["admin-categories"]);
    },
  });
};

export const useUpdateCategory = () => {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: ({ id, data }) =>
      axiosInstance.patch(`/categories/${id}/`, data),

    onSuccess: () => {
      queryClient.invalidateQueries(["admin-categories"]);
    },
  });
};

export const useDeleteCategory = () => {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (id) =>
      axiosInstance.delete(`/categories/${id}/`),

    onSuccess: () => {
      queryClient.invalidateQueries(["admin-categories"]);
    },
  });
};

// NOTE: there is deliberately no `useCreateSkill` here.
//
// This file used to export a second one that posted to `/categories/skills/`,
// which is **not a route** -- `CategoriesController` registers
// `[HttpPost("skills/create")]`, so every call 404'd. The live hook is
// `skillMutations.js`'s `useCreateSkill`, which delegates to
// `categoryApi.createSkill` -> `POST /categories/skills/create/`. That is the one
// `AddSkillsModal.jsx` imports. Keeping a divergent duplicate here was a trap:
// importing from this file silently produced a broken create.