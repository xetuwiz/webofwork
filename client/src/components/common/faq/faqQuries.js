import { useQuery } from "@tanstack/react-query";
import { fetchFAQs } from "./faqApi.js";
export const useFAQs = (params = {}) => useQuery({ queryKey: ["faqs", params], queryFn: () => fetchFAQs(params) });
