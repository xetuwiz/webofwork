import { useQuery, keepPreviousData } from "@tanstack/react-query";
import { fetchUsers } from "./userManagementApi";

export const useAdminUsers = ({ page = 1, page_size = 20, search = "", status = "" } = {}) => {
  return useQuery({
    queryKey: ["admin-users", { page, page_size, search, status }],
    queryFn: () => fetchUsers({ page, page_size, search, status }),
    // `keepPreviousData` was the react-query v4 spelling and was removed in v5,
    // so it was silently ignored -- every page change blanked the table while the
    // next request was in flight. `placeholderData: keepPreviousData` is the v5
    // equivalent and keeps the current rows on screen.
    placeholderData: keepPreviousData,
    staleTime: 60 * 1000,
  });
};
