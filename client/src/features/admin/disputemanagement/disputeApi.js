import axiosInstance from "../../../api/axiosInstance";

/**
 * Takes params now. It previously ignored its arguments and called the endpoint
 * bare, which meant the server had no page/search/status to work with even after
 * the query hook was fixed.
 */
export const fetchAdminDisputes = async (params = {}) => {
  const { data } = await axiosInstance.get(`/contracts/admin/disputes/`, { params });
  return data;
};

export const fetchAdminDisputeDetail = async (id) => {
  const { data } = await axiosInstance.get(`/contracts/admin/disputes/${id}/`);
  return data;
};

export const resolveDispute = async ({ id, status, admin_notes }) => {
  const { data } = await axiosInstance.post(
    `/contracts/admin/disputes/${id}/resolve/`,
    {
      status,
      admin_notes,
    },
  );
  return data;
};
