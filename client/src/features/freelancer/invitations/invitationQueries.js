import { useQuery, keepPreviousData } from '@tanstack/react-query';
import { getReceivedInvitations } from './invitationApi';

export const useReceivedInvitations = ({ status = '', page = 1, page_size = 20 } = {}) => {
  return useQuery({
    // This hook took no arguments, so InvitationsPage's
    // `useReceivedInvitations({ page, status })` had its object discarded and
    // the key was the constant ['received-invitations']. Both the status tab and
    // paging were therefore dead: React Query served the one cached response
    // forever, and the tab visibly did nothing because the filter never left
    // the browser.
    queryKey: ['received-invitations', { status, page, page_size }],
    queryFn: () => getReceivedInvitations({ status, page, page_size }),
    placeholderData: keepPreviousData,
    staleTime: 1000 * 60 * 5,
  });
};