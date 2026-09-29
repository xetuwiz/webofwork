import axiosInstance from "../../../api/axiosInstance";

export const fetchSupportTickets = async ({ page = 1, page_size = 20, status = "" } = {}) => {
  // Built with axios `params` rather than string concatenation: the old version
  // interpolated `status` straight into the URL, so a search term containing
  // `&` or `#` would silently truncate or mangle the request.
  const { data } = await axiosInstance.get(`/admin/support/tickets/`, {
    // `page_size` to match the rest of the API. SupportController.AdminListTickets
    // accepts both this and `pageSize`.
    params: { page, page_size, ...(status ? { status: status.toLowerCase() } : {}) },
  });
  return data;
};

export const fetchTicketDetails = async (id) => {
  const { data } = await axiosInstance.get(`/admin/support/tickets/${id}/`);
  return data;
};

export const updateTicketReply = async ({ id, admin_reply }) => {
  const { data } = await axiosInstance.patch(`/admin/support/tickets/${id}/`, {
    admin_reply,
  });
  return data;
};