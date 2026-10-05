/**
 * The notification matrix, as the server defines it.
 *
 * ## Why the vocabulary lives on the server
 *
 * Which channels a notification can travel over, and which role can receive it,
 * is a business rule. It used to be duplicated in three places that disagreed:
 * the SMS allow-list in `NotificationService` (which listed `new_message`, for
 * which no strategy existed), the push-title switch in
 * `NotificationPreferenceService`, and the flat mute list on the preference
 * model. A settings screen built from one list and enforced by another is how a
 * toggle ends up lying.
 *
 * So the server owns it (`NotificationCatalogue`) and sends the vocabulary down
 * with the preferences. This file holds only the one thing that is genuinely a
 * client concern: the order the channels are shown in.
 *
 * ## What the client still owns
 *
 * Nothing that affects enforcement. A client cannot turn on a channel the server
 * would ignore, because the server drops such a mute on write and the dispatcher
 * never consults the client.
 *
 * ## Display order
 *
 * SMS before push, because SMS is the channel most likely to be off by default
 * and therefore the one worth surfacing rather than burying under three others.
 */
export const CHANNEL_ORDER = ["in_app", "email", "sms", "push"];

/**
 * Builds a lookup of muted pairs.
 *
 * Keyed `"<type>|<channel>"` because the pair is the identity -- a type can be
 * muted on one channel and live on another, which is the whole point of the
 * per-channel column. A nested `Set` per type would be tidier to read but
 * invites the bug this feature exists to remove: treating "muted on email" as
 * "muted".
 */
export function buildMuteIndex(muted = []) {
  const index = new Set();
  for (const m of muted) {
    if (m?.type && m?.channel) index.add(`${m.type}|${m.channel}`);
  }
  return index;
}

export const isMuted = (index, type, channel) => index.has(`${type}|${channel}`);

/**
 * Groups the server's type list under its channels.
 *
 * The server sends each type with the channels it supports; a type therefore
 * appears under more than one channel, which is what the settings screen renders
 * as "this event, on these channels".
 */
export function groupTypesByChannel(types = []) {
  const grouped = Object.fromEntries(CHANNEL_ORDER.map((c) => [c, []]));
  for (const type of types) {
    for (const channel of type.channels ?? []) {
      if (grouped[channel]) grouped[channel].push(type);
    }
  }
  return grouped;
}
