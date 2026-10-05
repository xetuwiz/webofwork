import { useQuery, keepPreviousData } from "@tanstack/react-query";
import { fetchClientReviews, fetchClientSpendingSummary, getMyProfile } from "./profileApi";

export const useProfile = () => {
  return useQuery({
    queryKey: ["profile", "me"],
    queryFn: getMyProfile,
    staleTime: 1000 * 60 * 5, 
  });
};



export const useClientSpendingSummary = () => {
    return useQuery({
        queryKey: ["clientSpendingSummary"],
        queryFn: fetchClientSpendingSummary,
        staleTime: 1000 * 60 * 5, 
        retry: 1,
    });
};


export const useClientReviews = (userId, { page = 1, page_size = 20 } = {}) => {
  return useQuery({
    // This hook took only a userId, so the page state could never reach the
    // query -- ProfileOverview's review paginator moved a local number while
    // the list stayed on page 1.
    queryKey: ["clientReviews", userId, { page, page_size }],
    queryFn: () => fetchClientReviews(userId, { page, page_size }),
    placeholderData: keepPreviousData,
    enabled: Boolean(userId),
  });
};