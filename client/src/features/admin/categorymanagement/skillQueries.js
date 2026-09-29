import { useQuery, keepPreviousData } from "@tanstack/react-query";
import { fetchSkills } from "./categoryApi";

export const useSkills = ({ page = 1, page_size = 20, search = "" } = {}) =>
  useQuery({
    queryKey: ["admin-skills", { page, page_size, search }],
    queryFn: () => fetchSkills({ page, page_size, search }),
    // Correct: `fetchSkills` returns the whole axios response (unlike its
    // siblings, which unwrap), so the extra unwrap is needed here.
    select: (res) => res.data,
    placeholderData: keepPreviousData,
  });
