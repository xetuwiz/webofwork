import { useQuery } from "@tanstack/react-query";
import { fetchRecommendedFreelancers } from "./homePageApi.js";

export const useRecommendedFreelancers = () => useQuery({ queryKey: ["recommended-freelancers"], queryFn: fetchRecommendedFreelancers });
