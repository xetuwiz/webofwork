import axiosInstance from "../../../api/axiosInstance";

export const fetchTermsSections = async ({ page = 1, page_size = 20 } = {}) => {
      // Was a string-concatenated URL carrying no page size at all, so the
      // page-size selector had nothing to control. ContentController accepts
      // both `page_size` and `pageSize`.
      const { data } = await axiosInstance.get("/admin/cms/", {
        params: { category: "terms", page, page_size },
      });
      return data;
    };

export const createTermsSection = async (payload) => {
  const { data } = await axiosInstance.post(`/admin/cms/`, {
    ...payload,
    category: "terms",
  });
  return data;
};

export const updateTermsSection = async (payload) => {
  const { data } = await axiosInstance.patch(`/admin/cms/`, payload);
  return data;
};

export const deleteTermsSection = async (id) => {
  const { data } = await axiosInstance.delete(`/admin/cms/${id}/delete/`);
  return data;
};