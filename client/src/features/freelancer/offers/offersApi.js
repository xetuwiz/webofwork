import axiosInstance from '../../../api/axiosInstance'

export const fetchMyOffers = async ({ page = 1, page_size = 20 } = {}) => {
  // The endpoint used to return a bare array and ignore `page`; it now returns
  // the standard paged envelope and honours `page_size` (both spellings).
  const response = await axiosInstance.get("/contracts/my_offers/", {
    params: { page, page_size },
  });
  return response.data;
};


export const acceptContract = async ({ contractId, data }) => {
  const response = await axiosInstance.post(`/contracts/${contractId}/accept/`, data);
  return response.data;
};