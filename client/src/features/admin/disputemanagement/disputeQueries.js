import { useQuery } from "@tanstack/react-query";
import { fetchAdminDisputes, fetchAdminDisputeDetail } from "./disputeApi";

/**
 * This hook used to take no parameters at all, so DisputeList.jsx's
 * `useAdminDisputes(page, searchQuery, activeFilter)` passed three arguments that
 * were silently discarded, and the query key was a constant. Search, the status
 * filter and pagination were all dead: the key never changed, so React Query
 * served the one cached response forever.
 *
 * Every argument is now part of the query key, and the same params go to the
 * API. `placeholderData` keeps the previous page visible while a new one loads,
 * which stops the list flashing empty on every keystroke.
 */
export const useAdminDisputes = (params = {}) => {
  // Defaults merged under the caller's params, so a caller passing a full
  // `page_size` from the pagination footer wins, while a caller that passes
  // nothing still gets a working request.
  const query = { page: 1, page_size: 10, ...params };

  return useQuery({
    queryKey: ["admin", "disputes", query],
    queryFn: () => fetchAdminDisputes(query),
    placeholderData: (previous) => previous,
  });
};

export const useAdminDisputeDetail = (id) => {
  return useQuery({
    queryKey: ["admin", "disputes", id],
    queryFn: () => fetchAdminDisputeDetail(id),
    enabled: !!id,
  });
};