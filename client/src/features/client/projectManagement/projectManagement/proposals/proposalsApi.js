import axiosInstance from "../../../../api/axiosInstance";

export const getSentInvitations = async (projectId) => {
  const params = projectId ? { project_id: projectId } : {};
  const { data } = await axiosInstance.get('/projects/invitations/sent/', { params });
  return data;
};

export const getProposals = async (projectId, { page = 1, page_size = 20, status = '' } = {}) => {
  const { data } = await axiosInstance.get(`/projects/${projectId}/proposals/`, {
    // `page_size` to match the rest of the API; ProposalsController accepts both
    // this and `pageSize`.
    params: { page, page_size, status }
  });
  return data;
};

export const rejectProposal = async (proposalId) => {
  const res = await axiosInstance.patch(`/projects/proposals/${proposalId}/reject/`);
  return res.data;
};
