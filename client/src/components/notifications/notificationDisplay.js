export function formatNotification(notification) {
  return { ...notification, title: notification?.title || "Update", message: notification?.message || notification?.body || "No details provided.", isRead: Boolean(notification?.isRead) };
}
