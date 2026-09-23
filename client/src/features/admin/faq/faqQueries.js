import { useQuery } from "@tanstack/react-query";
import { fetchAdminFAQs } from "./faqApi.js";
export const useAdminFAQs = (params = {}) => useQuery({ queryKey: ["admin-faqs", params], queryFn: () => fetchAdminFAQs(params) });
