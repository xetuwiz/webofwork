import { useMutation, useQueryClient } from "@tanstack/react-query";
import { useDispatch, useSelector } from "react-redux";
import { setCredentials } from "../account/auth/authslice.js";
import { changePassword, updateProfile } from "./profileApi.js";

export const useUpdateProfile = () => {
  const dispatch = useDispatch();
  const queryClient = useQueryClient();
  const currentUser = useSelector((state) => state.auth.user);

  return useMutation({
    mutationFn: updateProfile,
    onSuccess: (updatedProfile) => {
      const user = { ...currentUser, ...updatedProfile };
      dispatch(setCredentials({ user }));
      queryClient.setQueryData(["profile", "me"], updatedProfile);
      queryClient.invalidateQueries({ queryKey: ["profile", "me"] });
    },
  });
};

export const useChangePassword = () =>
  useMutation({ mutationFn: changePassword });
