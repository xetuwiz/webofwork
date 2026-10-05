import { useEffect, useState } from 'react';
import { WifiOff, X } from 'lucide-react';

/**
 * Offline / reconnection banner.
 *
 * Requests fail when the connection drops, and the default reaction is a toast
 * or an error message telling the user something went wrong. That is
 * misleading: nothing is broken, the machine is offline. Reconnecting silently
 * once the connection is back avoids a burst of failed requests in the meantime,
 * because TanStack Query retries paused queries on reconnect.
 */
export default function OfflineBanner() {
  const [offline, setOffline] = useState(
    typeof navigator !== 'undefined' ? !navigator.onLine : false
  );
  const [dismissed, setDismissed] = useState(false);

  useEffect(() => {
    const goOnline = () => {
      setOffline(false);
      setDismissed(false);
    };
    const goOffline = () => setOffline(true);

    window.addEventListener('online', goOnline);
    window.addEventListener('offline', goOffline);
    return () => {
      window.removeEventListener('online', goOnline);
      window.removeEventListener('offline', goOffline);
    };
  }, []);

  if (!offline || dismissed) return null;

  return (
    <div
      role="status"
      aria-live="polite"
      className="fixed inset-x-0 top-0 z-banner flex items-center justify-center gap-2 bg-warning px-4 py-2 text-warning-fg shadow-lg"
    >
      <WifiOff className="h-4 w-4 shrink-0" aria-hidden="true" />
      <p className="text-xs font-semibold">
        You&apos;re offline. Changes won&apos;t be saved until you reconnect.
      </p>
      <button
        type="button"
        onClick={() => setDismissed(true)}
        aria-label="Dismiss offline notice"
        className="ml-1 rounded p-0.5 transition-colors hover:bg-warning/20 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-warning-fg"
      >
        <X className="h-3.5 w-3.5" aria-hidden="true" />
      </button>
    </div>
  );
}
