import { useQuery } from "@tanstack/react-query";
import { fetchTermsAndConditions } from "./termsAndConditionsApi.js";
export const useTermsAndConditions = () => useQuery({ queryKey: ["terms-and-conditions"], queryFn: fetchTermsAndConditions });
