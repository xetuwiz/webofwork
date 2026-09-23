import { useQuery } from "@tanstack/react-query";
import { fetchClientReviews, fetchClientSpendingSummary, getMyProfile } from "./profileApi.js";

export const useProfile = () =>
  useQuery({
    queryKey: ["profile", "me"],
    queryFn: getMyProfile,
    staleTime: 5 * 60 * 1000,
  });

export const useClientSpendingSummary = () =>
  useQuery({
    queryKey: ["client-spending-summary"],
    queryFn: fetchClientSpendingSummary,
  });

export const useClientReviews = (userId) =>
  useQuery({
    queryKey: ["client-reviews", userId],
    queryFn: () => fetchClientReviews(userId),
    enabled: userId === undefined || userId !== null,
  });
