import 'dart:async';

import 'package:audioplayers/audioplayers.dart';
import 'package:flutter/material.dart';
import 'package:provider/provider.dart';
import 'package:webofwork/helper/constant_helper.dart';
import 'package:webofwork/helper/extension/context_extension.dart';
import 'package:webofwork/helper/local_keys.g.dart';
import 'package:webofwork/helper/web_socket_service.dart';
import 'package:webofwork/models/notification_list_model.dart';
import 'package:webofwork/services/message_notification_count_service.dart';
import 'package:webofwork/services/notifications_list_service.dart';
import 'package:webofwork/views/notifications_list_view/notifications_list_view.dart';

/// Opens the `/ws/notifications` socket for the length of the signed-in session
/// and turns pushes into a badge bump, a list update and a banner.
///
/// Mounted once, from the shell (`OnboardingView`). Putting it there rather than
/// in `SignInService` is deliberate:
///  * it covers both entry points -- a fresh sign-in and a restored session on
///    cold start -- with one place, and
///  * the shell is exactly the signed-in lifetime, so `dispose` is the sign-out
///    boundary.
///
/// The previous implementation never opened this socket at all: `PusherHelper`
/// bailed on an empty app key, and nothing replaced it. Notifications therefore
/// arrived only when the user happened to pull the list, and the badge stayed at
/// zero until they did.
class NotificationRealtimeBinder extends StatefulWidget {
  const NotificationRealtimeBinder({super.key});

  @override
  State<NotificationRealtimeBinder> createState() => _NotificationRealtimeBinderState();
}

class _NotificationRealtimeBinderState extends State<NotificationRealtimeBinder> {
  StreamSubscription<SocketEvent>? _subscription;
  bool _started = false;

  @override
  void didChangeDependencies() {
    super.didChangeDependencies();
    // Guarded rather than done in `initState` because the socket instance lives
    // in `constant_helper`, which is only populated by `coreInit(context)` --
    // and that needs a usable `BuildContext`.
    if (_started) return;
    _started = true;
    WidgetsBinding.instance.addPostFrameCallback((_) => _connect());
  }

  @override
  void dispose() {
    _subscription?.cancel();
    // Only the notification socket is torn down. The room socket belongs to
    // whichever conversation is open and closes with it.
    webSocket?.disconnectNotifications();
    super.dispose();
  }

  Future<void> _connect() async {
    if (!mounted) return;
    final socket = webSocket;
    if (socket == null) return;
    if (getToken.isEmpty) return;

    // Subscribe before connecting: `WebSocketChannel.connect` is eager, so a row
    // published in the gap between connect and subscribe would be dropped.
    _subscription ??= socket.events.listen(_onEvent);

    try {
      await socket.connectNotifications();
    } catch (e) {
      debugPrint('[realtime] notification socket failed to open: $e');
    }

    // The badge has no dedicated route, so seed it from the list envelope. The
    // pushes only add to whatever the server already counted, and this fetch
    // does not mark anything read, so the number is correct on first paint.
    if (!mounted) return;
    await context.read<NotificationsListService>().seedBadge(context);
  }

  void _onEvent(SocketEvent event) {
    if (!mounted) return;
    if (event is! NotificationPushEvent) return;

    final row = AppNotification.fromJson(event.notification);
    if (row.id == 0 && row.message.isEmpty) return;

    final service = context.read<NotificationsListService>();
    final isNew = service.applyPush(row);
    if (!isNew) return;

    _playSound();

    final counts = context.read<MessageNotificationCountService>();
    if (!row.isRead) {
      counts.setNC(counts.notificationCount + 1);
    }

    _showBanner(row);
  }

  /// A short chime on a fresh push, mirroring the chat sounds in
  /// `conversation_service` (fire-and-forget, errors swallowed). `audioplayers`
  /// shares the platform audio session with the chat tones, so the same
  /// best-effort rules apply: a locked session or a missing asset only skips
  /// the sound, never the row.
  void _playSound() {
    unawaited(() async {
      try {
        await AudioPlayer().play(AssetSource('audios/notification.wav'));
      } catch (_) {}
    }());
  }

  /// An in-app banner rather than a system notification.
  ///
  /// A `SnackBar` is the right control for the *foreground* case specifically: it
  /// is available the moment the shell is, and unlike a toast it can carry the
  /// "View" action that makes the push actionable. Showing a system notification
  /// here as well would double-notify the user while they are already looking at
  /// the screen.
  ///
  /// Background and terminated cases are FCM's job -- see
  /// `PushNotificationService`, which registers a channel and handles the
  /// `onMessageOpenedApp` tap.
  void _showBanner(AppNotification row) {
    final messengerContext = navigatorKey.currentContext;
    if (messengerContext == null) return;

    final scaffoldMessenger = ScaffoldMessenger.maybeOf(messengerContext);
    if (scaffoldMessenger == null) return;

    scaffoldMessenger
      ..hideCurrentSnackBar()
      ..showSnackBar(
        SnackBar(
          content: Text(
            row.message.isEmpty ? LocalKeys.newNotification : row.message,
            maxLines: 3,
            overflow: TextOverflow.ellipsis,
          ),
          duration: const Duration(seconds: 5),
          action: SnackBarAction(
            label: LocalKeys.view,
            onPressed: () {
              final nav = navigatorKey.currentContext;
              if (nav == null) return;
              nav.toNamed(NotificationListView.routeName);
            },
          ),
        ),
      );
  }

  @override
  Widget build(BuildContext context) => const SizedBox.shrink();
}
