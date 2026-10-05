import React, { useMemo, useState } from "react";
import { Bell, Mail, MessageSquare, Smartphone, Info, AlertTriangle, Loader2 } from "lucide-react";
import { toastSuccess } from "../../utils/toast";
import {
  useNotificationPreferences,
  useUpdateNotificationPreferences,
} from "../../components/notifications/notificationPreferenceQueries";
import {
  CHANNEL_ORDER,
  buildMuteIndex,
  isMuted as isCellMuted,
  groupTypesByChannel,
} from "./notificationMatrix";

/**
 * Notification preferences, shared by every role.
 *
 * ## Why this file is not under `features/freelancer/`
 *
 * It used to be. The API layer beneath it was already role-agnostic, but the
 * screen was only routed from `FreelancerRoutes`, so clients and admins had no
 * way to reach their own preferences at all -- a settings control they could
 * neither find nor use.
 *
 * ## What changed beyond the move
 *
 * The old screen had a flat "By type" list whose mute applied to *every* channel,
 * and said so in the copy. That is gone: mutes are now per (type, channel), and
 * the screen is arranged as a channel heading with the types nested under it, so a
 * switch always sits next to the channel it governs.
 *
 * Two related fixes came with it, both of which the old shape could not express:
 *
 *  - **A master switch.** Off silences everything while preserving the per-channel
 *    and per-type choices underneath, so turning notifications back on restores
 *    what the user had picked rather than resetting them to defaults.
 *  - **Email as a real channel.** The old screen deliberately had no email row,
 *    with a comment explaining that the SMTP sender was wired only to OTP and
 *    password reset, so a switch would "look real and do nothing". Email is now
 *    dispatched by `NotificationService`, so the switch is honest.
 *
 * ## Channels are the server's decision
 *
 * The list of channels, the types under each, and the labels all come from
 * `NotificationCatalogue` via the preferences response. SMS and push are
 * freelancer-only, so a client is never shown them at all.
 */
const CHANNEL_ICON = {
  in_app: Bell,
  email: Mail,
  sms: MessageSquare,
  push: Smartphone,
};

/** Maps a channel key to the boolean field on the preferences payload. */
const CHANNEL_FIELD = {
  in_app: "in_app_enabled",
  email: "email_enabled",
  sms: "sms_enabled",
  push: "push_enabled",
};

export default function NotificationPreferences() {
  const { data: prefs, isLoading, isError, refetch } = useNotificationPreferences();
  const update = useUpdateNotificationPreferences();

  // While a save is in flight the switches reflect the optimistic value, so a flip
  // does not visibly spring back and then forward.
  const [optimistic, setOptimistic] = useState(null);
  const view = optimistic ?? prefs ?? null;

  const mutedIndex = useMemo(
    () => buildMuteIndex(view?.muted ?? []),
    [view?.muted],
  );

  const grouped = useMemo(
    () => groupTypesByChannel(view?.types ?? []),
    [view?.types],
  );

  const save = (patch) => {
    if (!view) return;
    setOptimistic({ ...view, ...patch });
    update.mutate(patch, {
      onSuccess: (saved) => {
        setOptimistic(saved ?? null);
        toastSuccess("Notification preferences updated");
      },
      onError: () => {
        // The axios interceptor already surfaced the reason. Drop the optimistic
        // value so the switches return to what the server last confirmed rather
        // than showing a state that was never stored.
        setOptimistic(null);
      },
    });
  };

  const toggleChannel = (key) => {
    const field = CHANNEL_FIELD[key];
    if (!field) return;
    save({ [field]: !view[field] });
  };

  /**
   * A mute is a (type, channel) pair, so flipping one means recomputing the whole
   * set. `muted` replaces rather than merges on the server, so the set sent is
   * always the full truth.
   */
  const toggleCell = (type, channel) => {
    const current = view?.muted ?? [];
    const key = `${type}|${channel}`;
    const next = isCellMuted(mutedIndex, type, channel)
      ? current.filter((m) => `${m.type}|${m.channel}` !== key)
      : [...current, { type, channel }];
    save({ muted: next });
  };

  if (isLoading) {
    return (
      <div className="flex items-center justify-center py-16">
        <Loader2 className="w-8 h-8 text-brand animate-spin" aria-hidden="true" />
        <span className="sr-only">Loading notification preferences</span>
      </div>
    );
  }

  if (isError) {
    return (
      <div className="rounded-2xl border border-line bg-surface p-8 flex gap-3">
        <AlertTriangle className="text-danger shrink-0" size={20} aria-hidden="true" />
        <div>
          <h2 className="font-bold text-fg">Could not load your preferences</h2>
          <p className="text-fg-muted text-sm mt-1">
            Your current settings are unchanged. This is a problem on our side.
          </p>
          <button
            type="button"
            onClick={() => refetch()}
            className="mt-3 text-sm font-semibold text-brand hover:underline"
          >
            Try again
          </button>
        </div>
      </div>
    );
  }

  // No row on the server means the catalogue defaults, not "nothing on". The
  // server sends the resolved values, so this only guards an older server that
  // omits the new fields.
  const channels = (view?.channels ?? []).filter(
    (c) => CHANNEL_ORDER.includes(c.key) && CHANNEL_FIELD[c.key] in (view ?? {}),
  );
  const masterOn = view?.master_enabled !== false;

  return (
    <div className="max-w-3xl mx-auto">
      <div className="mb-8">
        <h1 className="text-2xl font-bold text-fg">Notification preferences</h1>
        <p className="text-fg-muted text-sm mt-1">
          Choose how we reach you, and which events are worth interrupting you for.
        </p>
      </div>

      {/* Master switch first: it is what someone reaches for when they want the
          app to stop, and burying it under four channel switches means they
          instead turn off all four. */}
      <div className="bg-surface rounded-2xl border border-line overflow-hidden">
        <label className="flex items-start gap-4 p-6 sm:p-8 cursor-pointer">
          <div className="flex-1 min-w-0">
            <div className="font-semibold text-fg text-sm">All notifications</div>
            <div className="text-fg-muted text-xs mt-1 leading-relaxed">
              {masterOn
                ? "Turn this off to silence every channel below without losing your individual choices."
                : "Everything is paused. Your per-channel and per-event choices are kept and will come back when you re-enable this."}
            </div>
          </div>
          <input
            type="checkbox"
            checked={masterOn}
            disabled={update.isPending}
            onChange={() => save({ master_enabled: !masterOn })}
            className="mt-1 h-5 w-5 rounded border-line-strong accent-[var(--brand)] cursor-pointer disabled:opacity-50 shrink-0"
          />
        </label>
      </div>

      {channels.map((channel) => {
        const Icon = CHANNEL_ICON[channel.key] ?? Bell;
        const field = CHANNEL_FIELD[channel.key];
        const channelOn = masterOn && !!view?.[field];
        const types = grouped[channel.key] ?? [];
        const offByDefault = channel.default_enabled === false;

        return (
          <div
            key={channel.key}
            className="bg-surface rounded-2xl border border-line overflow-hidden mt-6"
          >
            <div className="p-6 sm:p-8">
              <div className="flex items-start gap-4">
                <div className="w-10 h-10 rounded-full bg-brand-soft flex items-center justify-center text-brand-soft-fg shrink-0">
                  <Icon size={18} aria-hidden="true" />
                </div>
                <div className="flex-1 min-w-0">
                  <div className="font-semibold text-fg text-sm">
                    {channel.label}
                    {offByDefault && (
                      <span className="ml-2 text-[10px] font-bold uppercase tracking-wider text-fg-subtle">
                        Off by default
                      </span>
                    )}
                  </div>
                  <div className="text-fg-muted text-xs mt-1 leading-relaxed">
                    {channel.description}
                  </div>
                </div>
                <input
                  type="checkbox"
                  checked={!!view?.[field]}
                  disabled={update.isPending}
                  onChange={() => toggleChannel(channel.key)}
                  aria-label={`Enable ${channel.label}`}
                  className="mt-1 h-5 w-5 rounded border-line-strong accent-[var(--brand)] cursor-pointer disabled:opacity-50 shrink-0"
                />
              </div>
            </div>

            {types.length > 0 && (
              <>
                <div className="border-t border-line-soft px-6 sm:px-8 py-2 bg-surface-2">
                  <p className="text-[10px] font-bold uppercase tracking-widest text-fg-subtle">
                    Sent over {channel.label.toLowerCase()}
                  </p>
                </div>
                <div className="divide-y divide-line-soft">
                  {types.map((type) => {
                    const muted = isCellMuted(mutedIndex, type.type, channel.key);
                    // A cell under an off channel is not selectable. Showing it as
                    // "on" would be a lie: the user would unmute it, see no
                    // change, and conclude the app is broken.
                    const effective = !muted && channelOn;
                    const key = `${type.type}|${channel.key}`;

                    return (
                      <label
                        key={key}
                        className={`flex items-center justify-between gap-4 px-6 sm:px-8 py-3.5 ${
                          channelOn ? "cursor-pointer hover:bg-surface-2" : "opacity-60"
                        } transition-colors`}
                      >
                        <span className="min-w-0">
                          <span className="block text-sm text-fg">{type.label}</span>
                          <span className="block text-xs text-fg-muted mt-0.5">
                            {type.description}
                          </span>
                        </span>
                        <input
                          type="checkbox"
                          checked={effective}
                          disabled={!channelOn || update.isPending}
                          onChange={() => toggleCell(type.type, channel.key)}
                          aria-label={`${effective ? "Stop" : "Receive"} ${type.label} over ${channel.label.toLowerCase()}`}
                          className="h-5 w-5 rounded border-line-strong accent-[var(--brand)] cursor-pointer disabled:opacity-40 disabled:cursor-not-allowed shrink-0"
                        />
                      </label>
                    );
                  })}
                </div>
              </>
            )}
          </div>
        );
      })}

      {!view?.push_configured && (
        <div className="mt-6 p-4 rounded-xl border border-line bg-surface-2 flex gap-3">
          <AlertTriangle className="text-fg-muted shrink-0" size={20} aria-hidden="true" />
          <div className="text-sm text-fg-muted leading-relaxed">
            Push is not configured on the server, so nothing can be delivered yet.
            Your other channels are unaffected.
          </div>
        </div>
      )}

      <div className="mt-6 p-4 rounded-xl border border-brand-line bg-brand-soft flex gap-3">
        <Info className="text-brand-soft-fg shrink-0" size={20} aria-hidden="true" />
        <div className="text-sm text-brand-soft-fg leading-relaxed">
          <p className="font-bold mb-1">In-app needs the app open</p>
          In-app notifications are only there while you are signed in. Push reaches
          you when it is closed, so it is worth keeping something on it for
          anything time-sensitive.
        </div>
      </div>
    </div>
  );
}
