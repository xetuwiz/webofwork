import { useMutation, useQueryClient } from "@tanstack/react-query";
import { updateInvitationStatus } from "./invitationApi.js";
export const useUpdateInvitationStatus = () => { const client = useQueryClient(); return useMutation({ mutationFn: updateInvitationStatus, onSuccess: () => client.invalidateQueries({ queryKey: ["received-invitations"] }) }); };
