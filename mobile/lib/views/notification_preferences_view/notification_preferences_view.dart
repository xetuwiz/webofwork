import 'package:flutter/material.dart';
import 'package:provider/provider.dart';
import 'package:webofwork/helper/extension/context_extension.dart';
import 'package:webofwork/helper/extension/int_extension.dart';
// `showToast` on a String. Not to be confused with the type-label helper that
// used to sit next to this import.
import 'package:webofwork/helper/extension/string_extension.dart';
import 'package:webofwork/helper/local_keys.g.dart';
import 'package:webofwork/models/notification_preferences_model.dart';
import 'package:webofwork/services/notification_preferences_service.dart';
import 'package:webofwork/services/push_notification_service.dart';
import 'package:webofwork/utils/components/app_card.dart';
import 'package:webofwork/utils/components/custom_refresh_indicator.dart';
import 'package:webofwork/utils/components/navigation_pop_icon.dart';
import 'package:webofwork/views/notification_preferences_view/components/preference_widgets.dart';

/// Channel switches, with the per-type mutes nested under each channel.
///
/// ## What changed, and why
///
/// The old screen had two flat sections -- "Channels" (three switches) and "By
/// type" (one switch per type) -- and the type switch muted that type on *every*
/// channel at once. It said so in the section copy, which is an admission that
/// the UI could not express what the user actually wanted. There was no way to
/// say "don't email me about chat messages, but still show them in the app",
/// because the only mute was global.
///
/// The mute is now a (type, channel) pair, so a switch always sits next to the
/// channel it governs and the copy no longer has to hedge. The channels, the
/// types under each, and every label come from the server's
/// `NotificationCatalogue` via the preferences response; this file holds no
/// vocabulary of its own.
///
/// The old `notificationTypeLabel` helper in `helper/notification_type_labels.dart`
/// was a fifth copy of the server's type names and is now unreferenced.
class NotificationPreferencesView extends StatefulWidget {
  static const routeName = 'notification_preferences_view';
  const NotificationPreferencesView({super.key});

  @override
  State<NotificationPreferencesView> createState() =>
      _NotificationPreferencesViewState();
}

class _NotificationPreferencesViewState extends State<NotificationPreferencesView> {
  /// The server's last answer. Null until the first load resolves, which is what
  /// distinguishes "still loading" from "loaded, everything on".
  NotificationPreferences? _preferences;

  /// Fallback vocabulary, used only when the preferences request failed. The
  /// response normally carries the types, so a separate fetch is no longer the
  /// primary path.
  List<NotificationTypeOption> _fallbackTypes = const [];

  bool _loading = true;
  bool _saving = false;

  /// Serialises writes. Every switch fires a request immediately, and a user
  /// flipping three in a row would otherwise race three PUTs -- the last write
  /// wins, so the two earlier ones silently revert.
  bool _writeInFlight = false;

  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addPostFrameCallback((_) => _load());
  }

  Future<void> _load() async {
    setState(() => _loading = true);
    final service = context.read<NotificationPreferencesService>();
    final results = await Future.wait([
      service.fetchPreferences(),
      service.loadTypeVocabulary(),
    ]);

    if (!mounted) return;
    setState(() {
      _preferences = results[0] as NotificationPreferences?;
      // The service caches the vocabulary; read it back rather than threading the
      // list through the future's value type.
      _fallbackTypes = service.typeVocabulary;
      _loading = false;
    });
  }

  NotificationPreferences get _current =>
      _preferences ?? const NotificationPreferences();

  /// Optimistically applies [next] so the switch moves immediately, then PUTs.
  ///
  /// On failure the optimistic value is rolled back -- a switch that stays on
  /// after the server refused it is worse than one that visibly snaps back.
  ///
  /// Only the field that changed is sent. Sending the whole object would mean a
  /// stale read could overwrite a preference the user changed on another device
  /// in between, and the server treats `muted` as a replacement, so a stale
  /// mute list would silently undo every mute.
  Future<void> _apply(
    NotificationPreferences next, {
    bool? masterEnabled,
    bool? inAppEnabled,
    bool? pushEnabled,
    bool? smsEnabled,
    bool? emailEnabled,
    List<NotificationMute>? muted,
  }) async {
    if (_writeInFlight) return;
    final previous = _preferences;
    setState(() {
      _writeInFlight = true;
      _saving = true;
      _preferences = next;
    });

    final saved = await context.read<NotificationPreferencesService>().updatePreferences(
          masterEnabled: masterEnabled,
          inAppEnabled: inAppEnabled,
          pushEnabled: pushEnabled,
          smsEnabled: smsEnabled,
          emailEnabled: emailEnabled,
          muted: muted,
        );

    if (!mounted) return;
    setState(() {
      _writeInFlight = false;
      _saving = false;
      // Fall back to the previous state on failure, not to `next`: a rejected
      // write must not leave a value on screen that the server never accepted.
      // The network layer has already toasted the reason.
      _preferences = saved ?? previous;
    });
    if (saved != null) LocalKeys.notificationPreferencesUpdated.showToast();
  }

  /// Flips one channel.
  ///
  /// Push is special: turning it *on* may need the OS permission, so it cannot
  /// just write a boolean.
  Future<void> _toggleChannel(String channelKey, bool value) async {
    switch (channelKey) {
      case 'in_app':
        await _apply(
          _current.copyWith(inAppEnabled: value),
          inAppEnabled: value,
        );
      case 'email':
        await _apply(
          _current.copyWith(emailEnabled: value),
          emailEnabled: value,
        );
      case 'sms':
        await _apply(
          _current.copyWith(smsEnabled: value),
          smsEnabled: value,
        );
      case 'push':
        await _togglePush(value);
    }
  }

  Future<void> _togglePush(bool value) async {
    if (value) {
      final push = context.read<PushNotificationService>();
      await push.initPush(requestPermission: true);
      if (!mounted) return;

      if (!push.available) {
        LocalKeys.pushUnavailable.showToast();
        return;
      }
      if (push.permissionDenied) {
        LocalKeys.pushPermissionBlocked.showToast();
        // The switch stays off: the server would be told push is on while the OS
        // is dropping every notification, which is the most confusing possible
        // state to be in.
        return;
      }
      // The token is registered by `initPush`; re-registering here covers the
      // case where it was already granted and nothing changed.
      await push.reregister();
    }

    if (!mounted) return;
    await _apply(
      _current.copyWith(pushEnabled: value),
      pushEnabled: value,
    );
  }

  /// Flips one (type, channel) cell.
  ///
  /// The whole mute list is sent, because the server replaces it. Sending a
  /// single pair would be a no-op; sending a merged list would mean a user could
  /// never un-mute anything.
  Future<void> _toggleCell(String type, String channel) async {
    final next = _current.toggle(type, channel);
    await _apply(_current.copyWith(muted: next), muted: next);
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(
        leading: const NavigationPopIcon(),
        title: Text(LocalKeys.notificationPreferences),
      ),
      body: _loading
          ? Center(
              child: CircularProgressIndicator(
                color: context.dProvider.primaryColor,
              ),
            )
          : _preferences == null
              // The channel list comes from the server, so a failed request leaves
              // genuinely nothing to render. Still wrapped in the refresh indicator
              // rather than a bare `Center`, because that is where the retry
              // gesture lives -- a strip telling the user to pull down, on a screen
              // with nothing to pull, is a worse dead end than the failure.
              ? CustomRefreshIndicator(
                  onRefresh: _load,
                  child: ListView(
                    physics: const AlwaysScrollableScrollPhysics(),
                    padding: const EdgeInsets.all(20),
                    children: const [
                      PreferenceNotice(
                        message: 'Could not load your notification settings. '
                            'Pull down to try again.',
                        icon: Icons.cloud_off_outlined,
                      ),
                    ],
                  ),
                )
              : CustomRefreshIndicator(
                  onRefresh: _load,
                  child: ListView(
                    physics: const AlwaysScrollableScrollPhysics(),
                    padding: const EdgeInsets.all(20),
                    children: [
                      _buildPushNotice(),
                      20.toHeight,
                      _buildMaster(),
                      20.toHeight,
                      ..._buildChannelGroups(),
                      20.toHeight,
                      _buildExplain(),
                    ],
                  ),
                ),
    );
  }

  /// The master switch sits above the channels rather than at the bottom.
  ///
  /// It is what someone reaches for when they want the app to stop, and burying
  /// it under three channel switches means they instead turn off all three --
  /// and lose the per-type choices underneath in the process.
  Widget _buildMaster() {
    final on = _current.masterEnabled;
    return AppCard(
      child: PreferenceSwitchTile(
        title: 'All notifications',
        subtitle: on
            ? 'Turn this off to silence every channel below without losing your '
                'individual choices.'
            : 'Everything is paused. Your per-channel and per-event choices are '
                'kept and come back when you re-enable this.',
        value: on,
        leading: _iconFor('in_app'),
        onChanged: _busy
            ? null
            : () => _apply(
                  _current.copyWith(masterEnabled: !on),
                  masterEnabled: !on,
                ),
      ),
    );
  }

  bool get _busy => _saving || _writeInFlight;

  /// One card per channel: the channel's own switch, then the types it can carry.
  List<Widget> _buildChannelGroups() {
    // Prefer the server's channel list. A user with no saved row still gets one,
    // so the empty case here means an older server that predates the field.
    final channels = _current.channels;
    final widgets = <Widget>[];

    for (var i = 0; i < channels.length; i++) {
      final channel = channels[i];
      final on = _current.isChannelOn(channel.key);
      final types = _typesFor(channel.key);

      // Push is the one channel that can be undeliverable for a reason the user
      // cannot fix here, so it is disabled rather than offered and ignored.
      final pushBlocked = channel.key == 'push' && !_current.pushConfigured;

      widgets.add(AppCard(
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            PreferenceSwitchTile(
              title: channel.label,
              subtitle: pushBlocked
                  ? 'Not available right now: the server has no push credentials '
                      'configured, so enabling this would not deliver anything.'
                  : channel.description,
              value: !_current.masterEnabled ? false : on,
              leading: _iconFor(channel.key),
              onChanged: _busy || pushBlocked ? null : () => _toggleChannel(channel.key, !on),
            ),
            if (types.isNotEmpty) ...[
              12.toHeight,
              AppSectionHeader(
                title: 'Sent over ${channel.label.toLowerCase()}',
              ),
              ...types.map(
                (type) => PreferenceSwitchTile(
                  title: type.label,
                  subtitle: type.description,
                  // A cell under an off channel is not selectable. Rendering it
                  // as "on" would be a lie: the user would unmute it, see no
                  // change, and conclude the app is broken.
                  value: on && !_current.isMuted(type.type, channel.key),
                  onChanged: !on || _busy ? null : () => _toggleCell(type.type, channel.key),
                ),
              ),
            ],
          ],
        ),
      ));
      if (i < channels.length - 1) widgets.add(20.toHeight);
    }

    return widgets;
  }

  /// The types a channel can carry.
  List<NotificationTypeOption> _typesFor(String channelKey) {
    final fromPreferences = _current.typesFor(channelKey);
    if (fromPreferences.isNotEmpty) return fromPreferences;
    // A failed preferences response leaves `types` empty but the vocabulary
    // fetch may have succeeded. The channels list is then unknown, so a fallback
    // type with no channel list belongs under in-app only.
    if (channelKey != 'in_app') return const [];
    return _fallbackTypes;
  }

  Widget? _iconFor(String channelKey) => switch (channelKey) {
        'in_app' => const Icon(Icons.notifications_outlined, size: 20),
        'email' => const Icon(Icons.mail_outline, size: 20),
        'sms' => const Icon(Icons.sms_outlined, size: 20),
        'push' => const Icon(Icons.smartphone_outlined, size: 20),
        _ => null,
      };

  Widget _buildPushNotice() {
    final push = context.watch<PushNotificationService>();

    // Checked before the device-side conditions. This is the failure with no other
    // symptom: the server accepts the switch, the token registers, and the push
    // is never sent -- so without this the screen would claim push is on.
    if (!_current.pushConfigured) {
      return const PreferenceNotice(
        message: 'Push is not configured on the server, so nothing can be '
            'delivered yet. Your other channels are unaffected.',
        icon: Icons.cloud_off_outlined,
      );
    }
    if (!push.available) {
      return const PreferenceNotice(
        message: 'Push notifications are not available on this device. '
            'Your other channels are unaffected.',
        icon: Icons.cloud_off_outlined,
      );
    }
    if (push.permissionDenied) {
      return PreferenceNotice(
        message: 'Notifications are turned off for this app. Enable them in '
            'Android settings to receive push.',
        icon: Icons.notifications_off_outlined,
        background: context.dProvider.warningColor.withValues(alpha: 0.10),
        foreground: context.dProvider.warningColor,
      );
    }
    return const SizedBox.shrink();
  }

  Widget _buildExplain() {
    return const PreferenceNotice(
      message: 'Push is delivered by Google even when WebOfWork is closed. '
          'In-app notifications and the badge need the app open.',
      icon: Icons.lightbulb_outline,
    );
  }
}
