import { useEffect } from 'react';
import { useQueryClient } from '@tanstack/react-query';
import { useSelector } from 'react-redux';

/**
 * Lazily created, and only in a DOM.
 *
 * This was `const notificationSound = new Audio(...)` at module scope, which is
 * an import side effect: importing this module outside a browser -- a node test
 * runner, SSR, Storybook -- threw `ReferenceError: Audio is not defined` before
 * any test could run. It also built the element for consumers that never
 * receive a notification.
 *
 * `play()` already swallowed autoplay rejections, so no behaviour is lost.
 */
let notificationSound;
function getNotificationSound() {
  if (typeof Audio === 'undefined') return null;
  if (!notificationSound) {
    notificationSound = new Audio('/notification-pop.mp3');
  }
  return notificationSound;
}

// Reconnect backoff. The first attempt is immediate (the effect already runs on
// mount); after a drop we wait, then double up to MAX_DELAY.
const RETRY_BASE_MS = 1000;
const RETRY_MAX_MS = 30000;

/**
 * Live notification feed over the WebSocket.
 *
 * Two bugs this replaces:
 *
 * 1. It wrote the optimistic update into `{ results: [...] }`, but the list
 *    endpoint returns `{ data: { unread_count, total_count, items } }` and every
 *    reader uses `data.items` / `data.unread_count`. The prepend was a dead
 *    write -- a notification arriving over the socket never appeared until the
 *    next refetch.
 *
 * 2. It set only `onmessage`. No `onclose`/`onerror`, so once the socket dropped
 *    -- expired token, server restart, laptop sleep -- it never came back. The
 *    only thing that reconnected it was `accessToken` changing, which happens
 *    only when some *other* request 401s and triggers a refresh. An expired
 *    token 401s the upgrade itself, so the socket could sit dead while the page
 *    looked fine.
 */
export const useNotificationSocket = (userId) => {
  const queryClient = useQueryClient();
  const accessToken = useSelector((state) => state.auth.accessToken);
  const currentRole = useSelector((state) => state.auth.user?.current_role);

  useEffect(() => {
    if (!userId || !accessToken) return;

    let socket = null;
    let retryTimer = null;
    let attempt = 0;
    let disposed = false;

    const connect = () => {
      if (disposed) return;

      socket = new WebSocket(`${import.meta.env.VITE_WS_URL}/notifications/?token=${accessToken}`);

      socket.onopen = () => {
        attempt = 0;
      };

      socket.onmessage = (event) => {
        let newNotif;
        try {
          newNotif = JSON.parse(event.data);
        } catch {
          return;
        }
        // The server pushes the same projection the list returns, so a pushed row
        // is the row itself and has no `type` envelope. Ignore anything that
        // isn't a notification rather than treating it as one.
        if (!newNotif?.id || !newNotif?.notification_type) return;

        // `target_role` is stored lower-case in the database; compare
        // case-insensitively rather than trusting both sides to agree.
        const role = String(currentRole || '').toLowerCase();
        const target = String(newNotif.target_role || '').toLowerCase();
        const forThisRole = !target || target === role;

        if (forThisRole && !newNotif.is_read) {
          const sound = getNotificationSound();
          if (sound) {
            sound.currentTime = 0;
            // Autoplay is routinely blocked before any user gesture; the
            // rejection is expected and not worth surfacing.
            sound.play().catch(() => {});
          }
        }

        queryClient.setQueryData(['notifications', false], (oldData) => {
          if (!forThisRole) return oldData;

          const prevItems = oldData?.data?.items ?? [];
          if (prevItems.some((n) => n.id === newNotif.id)) return oldData;

          const wasUnread = newNotif.is_read ? 0 : 1;
          return {
            ...oldData,
            data: {
              ...oldData?.data,
              items: [newNotif, ...prevItems],
              unread_count: (oldData?.data?.unread_count ?? 0) + wasUnread,
              total_count: (oldData?.data?.total_count ?? prevItems.length) + 1,
            },
          };
        });

        queryClient.invalidateQueries(['notifications']);
      };

      const scheduleReconnect = () => {
        if (disposed) return;
        const delay = Math.min(RETRY_BASE_MS * 2 ** attempt, RETRY_MAX_MS);
        attempt += 1;
        retryTimer = setTimeout(connect, delay);
      };

      socket.onerror = () => {
        // `close` always follows `error`; let it own the retry so we don't
        // schedule two connections for one failure.
      };

      socket.onclose = scheduleReconnect;
    };

    connect();

    return () => {
      disposed = true;
      if (retryTimer) clearTimeout(retryTimer);
      if (socket) {
        // Drop the handlers first: closing on purpose must not schedule a retry.
        socket.onclose = null;
        socket.onerror = null;
        socket.onmessage = null;
        if (socket.readyState === WebSocket.OPEN || socket.readyState === WebSocket.CONNECTING) {
          socket.close();
        }
      }
    };
  }, [userId, accessToken, queryClient, currentRole]);
};
