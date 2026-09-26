import { useMutation, useQueryClient } from '@tanstack/react-query';
import { updateInvitationStatus } from './invitationApi';
import { toastApiError, toastSuccess } from "../../../utils/toast";

export const useUpdateInvitationStatus = () => {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: updateInvitationStatus,
    onSuccess: (data) => {
      queryClient.invalidateQueries({ queryKey: ['received-invitations'] });

      const statusText = data.status.charAt(0).toUpperCase() + data.status.slice(1);
      toastSuccess(`Invitation ${statusText} successfully!`);
    },
    // See `payoutMutations.js` for why the shared helper is used instead of a
    // hand-rolled `data?.error` read. This matters here specifically: responding
    // to an invitation is refused for real reasons ("You cannot accept an
    // invitation for a project you have already proposed on."), and they arrive
    // as ProblemDetails under `detail`.
    onError: (error) => toastApiError(error, "Failed to update invitation")
  });
};