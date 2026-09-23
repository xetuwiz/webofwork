import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { createInvitation, fetchEligibleJobs, getFreelancerDetails, getFreelancerList } from "./findTalentApi.js";

export const useFreelancerList = (params = {}) => useQuery({ queryKey: ["freelancers", params], queryFn: () => getFreelancerList(params) });
export const useFreelancerDetails = (id) => useQuery({ queryKey: ["freelancer", id], queryFn: () => getFreelancerDetails(id), enabled: Boolean(id) });
export const useEligibleProjects = (enabled = true) => useQuery({ queryKey: ["eligible-projects"], queryFn: fetchEligibleJobs, enabled });
export const useSendInvitation = (options = {}) => {
  const client = useQueryClient();
  return useMutation({ mutationFn: createInvitation, ...options, onSuccess: (...args) => { client.invalidateQueries({ queryKey: ["invitations"] }); options.onSuccess?.(...args); } });
};
