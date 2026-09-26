import { useQuery, keepPreviousData } from "@tanstack/react-query";
import { fetchAllCategories, fetchAllSkills, fetchCategories, fetchMyProjects, fetchProjectById, fetchSkills} from './ProjectApi';

export const useCategories = () => {
  return useQuery({
    queryKey: ["categories"],
    queryFn: fetchCategories,
    staleTime: 5 * 60 * 1000, 
  });
};

export const useSkills = () => {
  return useQuery({
    queryKey: ["skills"],
    queryFn: fetchSkills,
    staleTime: 5 * 60 * 1000,
  });
};

export const useAllSkillls = ()=>{
  return useQuery({
    queryKey:['all-skills'],
    queryFn:fetchAllSkills,
    staleTime:5 * 60 * 1000,
  })
}
export const useAllCategories = ()=>{
  return useQuery({
    queryKey:['all-categories'],
    queryFn:fetchAllCategories,
    staleTime:5 * 60 * 1000,
  })
}


export const useMyProjects = ({ page = 1, page_size = 20, status = '', search = '' } = {}) => {
  return useQuery({
    // Every filter is in the key, including the empty ones. `fetchMyProjects`
    // destructures its parameters back out of this key instead of closing over
    // them, so a field added here but not there is silently never sent -- the
    // two have to change together.
    queryKey: ['my-projects', { page, page_size, status, search }],
    queryFn: fetchMyProjects,
    placeholderData: keepPreviousData,
  })
}
export const useProjectDetails = (id) => {
  return useQuery({
    queryKey: ["project", id],
    queryFn: () => fetchProjectById(id),
    enabled: !!id,
  });
};