import { useQuery } from "@tanstack/react-query";
import { fetchMyOffers } from "./offersApi.js";
export const useFreelancerOffers = () => useQuery({ queryKey: ["freelancer-offers"], queryFn: fetchMyOffers });
export const useMyOffers = useFreelancerOffers;
