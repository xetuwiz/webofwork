import { useQuery, keepPreviousData } from "@tanstack/react-query";
import axiosInstance from "../../../api/axiosInstance";
import { fetchFreelancerReviews } from "./freelancerProfileApi";

export const useClientFreelancerProfile = (id) => {
  return useQuery({
    queryKey: ["freelancer-profile", id],
    queryFn: async () => {
      const { data } = await axiosInstance.get(`/profile/freelancer/profile/${id}/`);
      return data;
    },
    enabled: !!id, 
  });
};


export const useFreelancerReviews = (freelancerId, { page = 1, page_size = 20 } = {}) => {
  return useQuery({
    queryKey: ["reviews", freelancerId, { page, page_size }],
    queryFn: () => fetchFreelancerReviews(freelancerId, { page, page_size }),
    placeholderData: keepPreviousData,
    enabled: !!freelancerId,
  });
};