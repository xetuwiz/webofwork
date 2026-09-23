import { useQuery } from "@tanstack/react-query";
import { fetchGlobalSettings } from "./adminSettingsApi.js";
export const useGlobalSettings = () => useQuery({ queryKey: ["global-settings"], queryFn: fetchGlobalSettings });
