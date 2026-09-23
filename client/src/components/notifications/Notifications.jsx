import { useNotificationSocket } from "../../hooks/notification/useNotificationSocket.jsx";
import { useDeleteNotification, useMarkAllNotificationsRead, useMarkNotificationRead } from "./notificationMutations.js";
import { useNotifications } from "./notificationQueries.js";

const cardStyle = {
  border: "1px solid #e2e8f0",
  borderRadius: "16px",
  background: "#fff",
  padding: "1rem 1.1rem",
  marginBottom: "0.9rem",
  boxShadow: "0 8px 22px rgba(15, 23, 42, 0.04)",
};

const unreadStyle = {
  borderLeft: "4px solid #2563eb",
};

export default function Notifications() {
  const { data: notifications = [] } = useNotifications({ page: 1, limit: 20 });
  const markRead = useMarkNotificationRead();
  const markAllRead = useMarkAllNotificationsRead();
  const deleteNotification = useDeleteNotification();
  useNotificationSocket();

  const unreadCount = notifications.filter((notification) => !notification.isRead).length;

  return (
    <section style={{ padding: "0.25rem 0" }}>
      <div style={{ display: "flex", alignItems: "center", justifyContent: "space-between", gap: "1rem", marginBottom: "1.25rem" }}>
        <div>
          <span className="app-shell__eyebrow">Alerts</span>
          <h1 style={{ margin: "0.4rem 0 0" }}>Notifications</h1>
        </div>
        <button
          type="button"
          onClick={() => markAllRead.mutate()}
          disabled={markAllRead.isPending || unreadCount === 0}
          style={{ border: "1px solid #cbd5e1", borderRadius: "10px", padding: "0.7rem 1rem", background: "#fff", cursor: markAllRead.isPending || unreadCount === 0 ? "not-allowed" : "pointer" }}
        >
          Mark all read
        </button>
      </div>

      {notifications.length === 0 ? (
        <div style={{ ...cardStyle, color: "#475569" }}>No notifications yet.</div>
      ) : (
        notifications.map((notification) => (
          <article
            key={notification.id}
            style={{
              ...cardStyle,
              ...(notification.isRead ? {} : unreadStyle),
            }}
          >
            <div style={{ display: "flex", justifyContent: "space-between", gap: "1rem", alignItems: "start" }}>
              <div>
                <div style={{ fontWeight: 700, marginBottom: "0.3rem" }}>{notification.title ?? "Update"}</div>
                <div style={{ color: "#475569", lineHeight: 1.5 }}>{notification.message ?? notification.body ?? "No details provided."}</div>
              </div>
              <div style={{ display: "flex", gap: "0.5rem", alignItems: "center" }}>
                {!notification.isRead && (
                  <button
                    type="button"
                    onClick={() => markRead.mutate(notification.id)}
                    disabled={markRead.isPending}
                    style={{ border: "1px solid #cbd5e1", borderRadius: "8px", background: "#fff", padding: "0.45rem 0.7rem", cursor: markRead.isPending ? "not-allowed" : "pointer" }}
                  >
                    Read
                  </button>
                )}
                <button
                  type="button"
                  onClick={() => deleteNotification.mutate(notification.id)}
                  disabled={deleteNotification.isPending}
                  style={{ border: "1px solid #fecaca", borderRadius: "8px", background: "#fff1f2", color: "#991b1b", padding: "0.45rem 0.7rem", cursor: deleteNotification.isPending ? "not-allowed" : "pointer" }}
                >
                  Delete
                </button>
              </div>
            </div>
            <div style={{ marginTop: "0.75rem", fontSize: "0.78rem", color: "#64748b" }}>
              {notification.createdAt ? new Date(notification.createdAt).toLocaleString() : "Recently"}
            </div>
          </article>
        ))
      )}
    </section>
  );
}
