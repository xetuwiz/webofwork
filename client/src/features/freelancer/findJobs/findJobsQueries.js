import { useQuery, keepPreviousData } from "@tanstack/react-query";
import { fetchFreelancerProjects } from "./findJobsAPI";

export const useFreelancerProjects = ({
  page = 1,
  page_size = 20,
  search = '',
  category = '',
  skill = '',
  min_price = '',
  max_price = '',
  delivery_days = '',
} = {}) => {
  return useQuery({
    // Every filter is in the key, including the empty ones. This is the one that
    // matters most on this page: with six filters, a partial key means two
    // different filter combinations render each other's cached results.
    queryKey: [
      "freelancer-projects",
      { page, page_size, search, category, skill, min_price, max_price, delivery_days },
    ],
    queryFn: () =>
      fetchFreelancerProjects({ page, page_size, search, category, skill, min_price, max_price, delivery_days }),
    placeholderData: keepPreviousData,
    staleTime: 60 * 1000,
  });
};
