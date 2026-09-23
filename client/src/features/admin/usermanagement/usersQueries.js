import { useQuery } from "@tanstack/react-query";
import { fetchUsers } from "./userManagementApi.js";
export const useAdminUsers = (params = {}) => useQuery({ queryKey: ["admin-users", params], queryFn: () => fetchUsers(params) });
