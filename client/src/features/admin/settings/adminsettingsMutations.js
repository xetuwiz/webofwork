import { useMutation, useQueryClient } from '@tanstack/react-query';
import { updateGlobalSettings } from './adminSettingsApi';
import { toastError, toastSuccess } from "../../../utils/toast";
export const useUpdateSettings = () => {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: updateGlobalSettings,
    onSuccess: (data) => {
      queryClient.setQueryData(['globalSettings'], data);
      toastSuccess('Settings updated successfully');
    },
    onError: (error) => {
      const message = error.response?.data?.message || 'Failed to update settings';
      toastError(message);
    }
  });
};