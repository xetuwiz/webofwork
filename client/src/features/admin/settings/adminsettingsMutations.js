import { useMutation, useQueryClient } from "@tanstack/react-query";
import { updateGlobalSettings } from "./adminSettingsApi.js";
export const useUpdateSettings = () => { const client = useQueryClient(); return useMutation({ mutationFn: updateGlobalSettings, onSuccess: () => client.invalidateQueries({ queryKey: ["global-settings"] }) }); };
