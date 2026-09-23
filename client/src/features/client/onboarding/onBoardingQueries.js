import { useQuery } from "@tanstack/react-query";
import { fetchFreelancerProfile } from "./OnboardingAPI.js";

export const useFreelancerProfile = () => useQuery({ queryKey: ["freelancer-profile"], queryFn: fetchFreelancerProfile });
