import axiosInstance from "../../../api/axiosInstance";

/**
 * All four verbs live under `/content/admin/faq` (ContentController), which is
 * the resource's real owner and already returned the correct paged envelope.
 *
 * These used to call `/admin/faq` (AdminParityController), a duplicate of the
 * same four actions. The two had diverged: the duplicate's GET returned a bare
 * array while this page read `data.results` / `data.count`, so the admin table
 * rendered nothing and showed "No FAQs yet" for every admin -- while the public
 * /faq page, hitting a third route, listed the same rows happily. Two identical
 * URLs on different controllers is the kind of thing that makes a data bug look
 * like a frontend bug.
 *
 * The duplicate is deleted. Mobile only ever calls the public FAQ
 * (`AppUrls.faqUrl` -> /api/common/public/faq), so nothing else depended on it.
 */
export const fetchAdminFAQs = async ({ page = 1, page_size = 20 } = {}) => {
  const { data } = await axiosInstance.get("/content/admin/faq/", {
    params: { page, page_size },
  });
  return data;
};

export const createFAQ = async (formData) => {
  const { data } = await axiosInstance.post("/content/admin/faq/", formData);
  return data;
};

export const updateFAQ = async ({ id, ...formData }) => {
  const { data } = await axiosInstance.put(`/content/admin/faq/${id}/`, formData);
  return data;
};

export const deleteFAQ = async (id) => {
  const { data } = await axiosInstance.delete(`/content/admin/faq/${id}/`);
  return data;
};