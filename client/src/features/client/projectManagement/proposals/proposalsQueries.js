import { useQuery, keepPreviousData } from "@tanstack/react-query";
import { getProposals, getSentInvitations } from "./proposalsApi";

export const useSentInvitations = (projectId) => {
  return useQuery({
    queryKey: ['sent-invitations', projectId], 
    queryFn: () => getSentInvitations(projectId),
    staleTime: 1000 * 60 * 5,
    enabled: !!projectId, 
  });
};

export const useProposals = (projectId, { page = 1, page_size = 20, status = '' } = {}) => {
  return useQuery({
    // `page` was missing from this key, so paging a client's proposal list
    // served the one cached response for page 1 forever -- the arrows moved a
    // number in local state while the list never changed.
    queryKey: ['proposals', projectId, { page, page_size, status }],
    queryFn: () => getProposals(projectId, { page, page_size, status }),
    placeholderData: keepPreviousData,
    staleTime: 1000 * 60 * 5,
  });
};