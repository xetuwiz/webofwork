import axiosInstance from "../../../api/axiosInstance";

export const fetchPublicFAQs = async ({ category = "", page = 1, page_size = 20 } = {}) => {
  const { data } = await axiosInstance.get("/common/public/faq/", {
    params: {
      // `undefined` rather than "" so axios omits the key entirely instead of
      // sending `category=` and splitting the query cache into two entries for
      // the same result set.
      category: category || undefined,
      page,
      // snake_case to match the rest of the API; the endpoint accepts both
      // spellings, so the page-size selector actually reaches the server.
      page_size,
    },
  });
  return data;
};
