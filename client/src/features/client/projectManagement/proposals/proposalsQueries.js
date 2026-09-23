import { useQuery } from "@tanstack/react-query";
import { getProposals, getSentInvitations } from "./proposalsApi.js";

export const useSentInvitations = (projectId) => useQuery({ queryKey: ["sent-invitations", projectId], queryFn: () => getSentInvitations(projectId), enabled: Boolean(projectId) });
export const useProposals = (projectId, params = {}) => useQuery({ queryKey: ["proposals", projectId, params], queryFn: () => getProposals(projectId, params), enabled: Boolean(projectId) });
