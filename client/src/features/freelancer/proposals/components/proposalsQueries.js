import { useQuery, keepPreviousData } from '@tanstack/react-query';
import { fetchProposals } from './proposalsApi';

export const useMyProposals = ({ page = 1, page_size = 20, status = '' } = {}) => {
  return useQuery({
    // Every filter is in the key, including the empty ones, so two different
    // filter states cannot collide on one cached response.
    queryKey: ['my-proposals', { page, page_size, status }],
    queryFn: () => fetchProposals({ page, page_size, status }),
    placeholderData: keepPreviousData,
    staleTime: 1000 * 60 * 5,
  });
};

