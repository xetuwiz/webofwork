import { useQuery } from "@tanstack/react-query";
import { fetchAdminDisputeDetail, fetchAdminDisputes } from "./disputeApi.js";
export const useAdminDisputes = (params = {}) => useQuery({ queryKey: ["admin-disputes", params], queryFn: () => fetchAdminDisputes(params) });
export const useAdminDisputeDetail = (id) => useQuery({ queryKey: ["admin-dispute", id], queryFn: () => fetchAdminDisputeDetail(id), enabled: Boolean(id) });
