import { useQuery } from "@tanstack/react-query";
import { fetchJobDetails } from "./jobDetailsApi.js";

export const useProjectDetails = (projectId) =>
  useQuery({
    queryKey: ["project", projectId],
    queryFn: () => fetchJobDetails(projectId),
    enabled: Boolean(projectId),
  });

export const useJobDetails = (projectId) => useProjectDetails(projectId);
