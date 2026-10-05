import axiosInstance from "../../../../api/axiosInstance";

export const createProposal = async (proposalData) => {
  const response = await axiosInstance.post('/projects/create-proposal/', proposalData);
  return response.data;
};


export const fetchProposals = async ({ page = 1, page_size = 20, status = '' } = {}) => {
  // `page_size` to match the rest of the API; ProposalsController accepts both
  // this and `pageSize`.
  const response = await axiosInstance.get('/projects/my-proposals/', {
    params: { page, page_size, status },
  });
  return response.data;
}

export const fetchPlatformFee = async ()=>{
  const {data} = await axiosInstance.get('common/platform-percentage/');
  return data.commission_percentage;
}