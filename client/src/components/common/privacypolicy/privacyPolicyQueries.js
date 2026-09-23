import { useQuery } from "@tanstack/react-query";
import { fetchPrivacyPolicy } from "./privacyPolicyApi.js";
export const usePrivacyPolicy = () => useQuery({ queryKey: ["privacy-policy"], queryFn: fetchPrivacyPolicy });
