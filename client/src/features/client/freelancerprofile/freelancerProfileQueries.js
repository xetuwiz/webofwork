import { useQuery } from "@tanstack/react-query";
import { fetchFreelancerProfile, fetchFreelancerReviews } from "./freelancerProfileApi.js";

export const useClientFreelancerProfile = (id) => useQuery({ queryKey: ["freelancer", id], queryFn: () => fetchFreelancerProfile(id), enabled: Boolean(id) });
export const useFreelancerReviews = (id, params = {}) => useQuery({ queryKey: ["freelancer-reviews", id, params], queryFn: () => fetchFreelancerReviews(id, params), enabled: Boolean(id) });
