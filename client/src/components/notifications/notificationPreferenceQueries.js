import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { useSelector } from "react-redux";
import {
  fetchNotificationPreferences,
  fetchNotificationPreferenceTypes,
  updateNotificationPreferences,
  fetchDevices,
} from "./notificationPreferencesApi";

export const PREFERENCES_KEY = ["notification-preferences"];
export const PREFERENCE_TYPES_KEY = ["notification-preference-types"];

export function useNotificationPreferences() {
  const { user } = useSelector((state) => state.auth);
  return useQuery({
    queryKey: PREFERENCES_KEY,
    queryFn: fetchNotificationPreferences,
    // Server-side preferences change rarely, and this screen is opened rarely.
    staleTime: 1000 * 60 * 5,
    enabled: !!user,
  });
}

export function useNotificationPreferenceTypes() {
  const { user } = useSelector((state) => state.auth);
  return useQuery({
    queryKey: PREFERENCE_TYPES_KEY,
    queryFn: fetchNotificationPreferenceTypes,
    staleTime: 1000 * 60 * 60,
    enabled: !!user,
  });
}

export function useDevices() {
  const { user } = useSelector((state) => state.auth);
  return useQuery({
    queryKey: ["notification-devices"],
    queryFn: fetchDevices,
    staleTime: 1000 * 60 * 5,
    enabled: !!user,
  });
}

/**
 * Applies a partial patch and writes the server's answer into the cache.
 *
 * The cache write is what keeps the switches from snapping back: without it the
 * optimistic value is replaced by the previous query result on the next render,
 * which reads as "the save failed" even when it succeeded.
 */
export function useUpdateNotificationPreferences() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: updateNotificationPreferences,
    onSuccess: (saved) => {
      if (saved) queryClient.setQueryData(PREFERENCES_KEY, saved);
    },
    onError: () => {
      // The axios interceptor already surfaces the reason. Roll the cache back to
      // whatever the server last confirmed rather than leaving a switch showing
      // a state that was never stored.
      queryClient.invalidateQueries({ queryKey: PREFERENCES_KEY });
    },
  });
}
