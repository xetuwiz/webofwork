import { useQuery } from "@tanstack/react-query";
import { fetchAdminReviews } from "./reviewApi.js";
export const useAdminReviews = (params = {}) => useQuery({ queryKey: ["admin-reviews", params], queryFn: () => fetchAdminReviews(params) });
