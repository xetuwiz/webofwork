import axiosInstance from "../../../api/axiosInstance";

export const fetchCategories = async () => {
  const res = await axiosInstance.get("/categories/");
  return res.data;
};

export const fetchSkills = async () => {
  const res = await axiosInstance.get("/categories/skills/");
  return res.data;
};
export const fetchAllSkills = async () => {
  const res = await axiosInstance.get("/categories/skills/",{params: {no_pagination: 'true'}});
  return res.data;
};

export const fetchAllCategories = async () => {
  const res = await axiosInstance.get("/categories/", {params: {no_pagination: 'true'}});
  
  return res.data; 
};

export const createProject = async (data) => {
  const res = await axiosInstance.post("/projects/create/", data);
  return res.data;
};

/**
 * Takes react-query's QueryFunctionContext and reads the filters back out of
 * `queryKey`.
 *
 * A workaround, not a design: the hook passes `queryFn: fetchMyProjects` with no
 * closure, so the filters had to come from somewhere, and the only place they
 * existed was the key. It works, but it means the key shape and this
 * destructuring must change together -- a field added to one and not the other
 * is silently never sent to the server.
 */
export const fetchMyProjects = async ({ queryKey }) => {
  const [_key, { page, page_size, status, search }] = queryKey;

  const res = await axiosInstance.get("/projects/", {
    // ProjectsController.List already takes `page_size`.
    params: { page, page_size, status, search },
  });

  return res.data;
};

export const fetchProjectById = async (id) => {  
  const res = await axiosInstance.get(`/projects/${id}/`);  
  return res.data;
};

export const updateProject = async ({id,data}) => {
  const res = await axiosInstance.patch(`/projects/${id}/`,data);
  return res.data;
};

export const deleteProject = async ({id}) =>{
  const res = await axiosInstance.delete(`/projects/${id}/`);
  return res.data;
}