import axiosInstance from "../../../api/axiosInstance";

export const fetchUsers = async ({ page = 1, page_size = 20, search = "", status = "" } = {}) => {
  const res = await axiosInstance.get("/admin/users/", {
    // snake_case to match the rest of the API. AdminUsersController accepts both
    // `page_size` and `pageSize`, so the page-size selector actually reaches
    // the server instead of being dropped by the model binder.
    params: { page, page_size, search, status },
  });
  return res.data;
};

export const toggleUserActive = async (userId) => {
  const res = await axiosInstance.patch(
    `/admin/users/${userId}/toggle-active/`
  );
  return res.data;
};

export const softDeleteUser = async (userId) => {
  const res = await axiosInstance.delete(
    `/admin/users/${userId}/soft-delete/`
  );
  return res.data;
};
