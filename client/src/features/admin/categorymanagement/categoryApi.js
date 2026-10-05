import axiosInstance from "../../../api/axiosInstance";

// Returns the whole axios response on purpose -- `useSkills` unwraps it with
// `select`, unlike the other queries in this folder.
export const fetchSkills = ({ page = 1, page_size = 20, search = "" } = {}) =>
  axiosInstance.get("/categories/skills/", {
    params: { page, page_size, search },
  });

export const createSkill = (data) =>
  axiosInstance.post("/categories/skills/create/", data);

export const updateSkill = (id, data) =>
  axiosInstance.patch(`/categories/skills/${id}/`, data);

export const deleteSkill = (id) =>
  axiosInstance.delete(`/categories/skills/${id}/`);

export const fetchAllCategories = async () => {
  const res = await axiosInstance.get("/categories/", {params: {no_pagination: 'true'}});
  
  return res.data; 
};