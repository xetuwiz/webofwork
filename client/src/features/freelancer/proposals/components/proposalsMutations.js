import { useMutation, useQueryClient } from '@tanstack/react-query';
import { createProposal } from './proposalsApi';
import { toastError, toastSuccess } from "../../../../utils/toast";

export const useSubmitProposal = () => {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: createProposal,
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['proposals'] });
      queryClient.invalidateQueries({queryKey:['project']})
      toastSuccess('Proposal submitted successfully!');
    },
    onError: (error) => {
    const errorData = error.response?.data
    const message = errorData?.non_field_errors?.[0] || 
                      errorData?.project?.[0] || 
                      errorData?.detail || 
                      'Failed to submit proposal';
      
      toastError(message);
    }
  });
};