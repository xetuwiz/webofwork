import axiosInstance from "../../api/axiosInstance";

/**
 * The controller answers `Ok(new { data = ... })`, so every read unwraps the single
 * `data` property of the response BODY.
 *
 * This takes the body, not the axios response. It used to take the axios response
 * AND read `.data.data`, which unwrapped twice: the body is already `data`, so
 * `.data.data` was undefined on every call. React Query then rejected the
 * undefined and the screen rendered its "Could not load your preferences" error
 * panel while the server had answered 200 three times.
 */
const unwrap = (body) => body?.data;

export const fetchNotificationPreferences = async () => {
  const { data } = await axiosInstance.get("/notifications/preferences/");
  return unwrap(data);
};

/**
 * The type and channel vocabulary for the caller's role, so the toggle list is
 * never hardcoded client-side and a new `NotificationType` on the server appears
 * without a release.
 *
 * The server now scopes this by role, so a client and an admin are sent different
 * lists rather than each filtering a shared one -- which is what let a client be
 * shown a "Payouts released" toggle for an event that can never reach them.
 */
export const fetchNotificationPreferenceTypes = async () => {
  const { data } = await axiosInstance.get("/notifications/preferences/types/");
  return data?.data ?? { channels: [], types: [] };
};

/**
 * Partial update. Omitted keys are left alone by the server.
 *
 * `muted` REPLACES the whole set rather than merging. The settings screen sends
 * the state it just rendered, and a merge would mean a user could never
 * un-mute anything: turning a switch off would add a row, and turning it back on
 * would leave the row in place.
 */
export const updateNotificationPreferences = async (patch) => {
  const { data } = await axiosInstance.put("/notifications/preferences/", patch);
  return unwrap(data);
};

export const fetchDevices = async () => {
  const { data } = await axiosInstance.get("/notifications/devices/");
  return data?.data ?? [];
};
