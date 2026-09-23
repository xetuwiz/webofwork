import { useQuery } from "@tanstack/react-query";
import { getReceivedInvitations } from "./invitationApi.js";
export const useReceivedInvitations = () => useQuery({ queryKey: ["received-invitations"], queryFn: getReceivedInvitations });
