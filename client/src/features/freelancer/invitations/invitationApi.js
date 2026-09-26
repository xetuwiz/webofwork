import axiosInstance from "../../../api/axiosInstance";

export const getReceivedInvitations = async ({ status = "", page = 1, page_size = 20 } = {}) => {
  // Was sending no query parameters at all, so the status tab and the page
  // number never reached the server even though InvitationsController has
  // always accepted both. `page_size` is the spelling the client uses
  // app-wide; the controller now accepts it too.
  const { data } = await axiosInstance.get('/projects/invitations/received/', {
    params: { ...(status ? { status } : {}), page, page_size },
  });
  return data;
};

export const updateInvitationStatus = async ({ id, status }) => {
  const { data } = await axiosInstance.patch(`/projects/invitations/${id}/update-status/`, {
    status: status 
  });
  return data;
};