import 'dart:async';
import 'dart:io';

import 'package:firebase_core/firebase_core.dart';
import 'package:firebase_messaging/firebase_messaging.dart';
import 'package:flutter/foundation.dart';
import 'package:flutter/material.dart';
import 'package:flutter_local_notifications/flutter_local_notifications.dart';
import 'package:permission_handler/permission_handler.dart';
import 'package:webofwork/data/network/network_api_services.dart';
import 'package:webofwork/helper/app_urls.dart';
import 'package:webofwork/helper/constant_helper.dart';
import 'package:webofwork/helper/local_keys.g.dart';
import 'package:webofwork/helper/query_string.dart' as query_string;

/// Entry point for a push that arrives while the app is **not** running.
///
/// Must be a top-level function with the `vm:entry-point` pragma: the Flutter
/// engine spins up a fresh isolate to run this, so it cannot close over anything
/// from the UI isolate or capture a reference to a service. That is why it
/// re-initialises Firebase rather than reaching for a singleton.
///
/// The body stays deliberately tiny. Anything slow here delays the notification
/// appearing, and Android gives a background handler roughly 10 seconds of
/// headroom before killing the isolate.
///
/// Note this handler only runs for **data-only** messages. The server sends
/// `notification` + `data` together, and Android displays the `notification`
/// block itself in the backgrounded and killed states without starting this
/// isolate. That is the intended path. It is still registered because a
/// data-only message is a legitimate thing for the server to send later, and an
/// unregistered handler means that path loses the message entirely.
@pragma('vm:entry-point')
Future<void> firebaseMessagingBackgroundHandler(RemoteMessage message) async {
  try {
    await Firebase.initializeApp();
  } catch (_) {
    // Already initialised in this isolate, or the config is unreadable. Either
    // way there is nothing to recover -- returning still lets Android show the
    // message's own `notification` block, which is the common path.
  }
  debugPrint('[push] background message ${message.messageId} '
      'data=${message.data}');

  // Defensive, and it matters: this is the only code that can run when the app
  // process did not exist, so it is the only chance to guarantee the channel
  // exists before Android tries to post to it. A notification posted to a
  // channel that was never created is *silently dropped* -- no tray entry, no
  // error, and nothing in the Dart logs to explain it.
  await PushNotificationService.ensureChannel();
}

/// Owns the FCM lifecycle: initialisation, the Android runtime permission, the
/// notification channel, token registration, and the three delivery callbacks.
///
/// ## Why this exists at all
///
/// The only delivery path before this was the WebSocket at `/ws/notifications`,
/// which is by definition alive only while the process is. A user who killed
/// the app, or whose phone dropped the socket in a lift, got no notification at
/// all -- the in-app SnackBar cannot appear without a running app. FCM covers
/// exactly that case.
///
/// The WebSocket is kept, deliberately: it is lower latency while the app is
/// open, and it is what drives the in-app badge. FCM is the floor, not the
/// replacement.
class PushNotificationService with ChangeNotifier {
  PushNotificationService();

  static const _channelId = 'webofwork_notifications';
  static const _channelName = 'Notifications';
  static const _channelDescription =
      'Contract activity, messages, payments and disputes.';

  final _local = FlutterLocalNotificationsPlugin();

  /// True once Firebase initialised *and* the OS granted POST_NOTIFICATIONS.
  /// The settings screen reads this to explain why a toggle has no effect.
  bool _ready = false;
  bool get ready => _ready;

  /// Set when the user has the switch off in-app but has not been asked by the
  /// OS yet -- the case where an explicit "enable" button on our own settings
  /// screen is more useful than a silent no-op.
  bool _permissionDenied = false;
  bool get permissionDenied => _permissionDenied;

  /// Fails on a device with no Play Services, a misconfigured
  /// `google-services.json`, or a build without the plugin linked. None of those
  /// should stop the app from starting.
  bool _available = false;
  bool get available => _available;

  String? _token;
  String? get token => _token;

  /// Guards against the double-registration this app would otherwise do: the
  /// shell mounts once per session, `onTokenRefresh` fires on rotation, and
  /// `initPush` is also called from a retry path. Each would hit the network.
  bool _initialising = false;

  /// Whether the three Firebase streams are already subscribed. Separate from
  /// `_initialising`, which only guards the *concurrent* case; this one guards the
  /// sequential re-run.
  bool _callbacksWired = false;

  /// Initialises Firebase, asks for permission, creates the channel, and
  /// registers the token. Safe to call more than once.
  Future<void> initPush({bool requestPermission = true}) async {
    if (_initialising) return;
    _initialising = true;
    try {
      try {
        await Firebase.initializeApp();
        _available = true;
      } catch (e) {
        // No google-services.json, or a Firebase project that does not carry this
        // package name. Logged rather than thrown: the app is fully usable
        // without push, and a hard failure here would block the splash screen.
        debugPrint('[push] Firebase unavailable: $e');
        _available = false;
        return;
      }

      await _createChannel();

      if (requestPermission) {
        await _requestPermission();
      }

      _wireCallbacks();

      final token = await FirebaseMessaging.instance.getToken();
      if (token != null && token.isNotEmpty) {
        await _registerToken(token);
      } else {
        debugPrint('[push] FCM returned no token '
            '(no Play Services, or the device is not signed in)');
      }
    } catch (e) {
      debugPrint('[push] init failed: $e');
    } finally {
      _initialising = false;
    }
  }

  /// Unregisters the current token. Called on sign-out so the next account does
  /// not inherit the previous one's notifications on a shared device.
  Future<void> unregister() async {
    final current = _token ?? await FirebaseMessaging.instance.getToken();
    if (current == null || current.isEmpty) return;
    try {
      await NetworkApiServices().deleteApi(
        // `query_string` rather than `Uri.encodeQueryComponent`: FCM tokens are
        // colon-delimited, and the two encoders disagree on `:` and on `+`, which
        // silently produces a token the server never matches.
        '${AppUrls.registerDeviceUrl}?${query_string.buildQueryString({'token': current})}',
        LocalKeys.pushNotifications,
        headers: acceptJsonAuthHeader,
      );
    } catch (e) {
      debugPrint('[push] unregister failed: $e');
    }
  }

  /// Re-registers after a sign-in, because a token registered while signed out
  /// is either absent or still pointing at the previous account.
  Future<void> reregister() async {
    try {
      final token = await FirebaseMessaging.instance.getToken();
      if (token != null && token.isNotEmpty) await _registerToken(token);
    } catch (e) {
      debugPrint('[push] re-register failed: $e');
    }
  }

  Future<void> _requestPermission() async {
    if (!Platform.isAndroid) return;

    final status = await Permission.notification.request();
    _permissionDenied = !status.isGranted;

    if (status.isPermanentlyDenied) {
      debugPrint('[push] POST_NOTIFICATIONS permanently denied; the user must '
          'enable it from Android settings');
    }

    // Ask FCM itself too. It is a no-op when already granted, and it is what
    // populates the FirebaseMessaging permission state used by
    // `getNotificationSettings` below.
    await FirebaseMessaging.instance.requestPermission(
      alert: true,
      badge: true,
      sound: true,
      provisional: false,
    );

    final settings = await FirebaseMessaging.instance.getNotificationSettings();
    _ready = settings.authorizationStatus == AuthorizationStatus.authorized ||
        settings.authorizationStatus == AuthorizationStatus.provisional;
  }

  /// Android requires a channel to exist *before* a message carrying that
  /// channel id is displayed, and a post to a missing channel is dropped
  /// silently -- no tray entry, no exception, nothing in the Dart log.
  ///
  /// Static so the background isolate can call it too. `createNotificationChannel`
  /// is idempotent (an existing channel's importance cannot be raised, but a
  /// re-create is a no-op), so calling it from both places is safe.
  static Future<void> ensureChannel() async {
    if (!Platform.isAndroid) return;

    final local = FlutterLocalNotificationsPlugin();
    await local.initialize(
      settings: const InitializationSettings(
        android: AndroidInitializationSettings('@drawable/notification_icon'),
      ),
      onDidReceiveNotificationResponse: (response) {
        debugPrint('[push] local notification tapped');
      },
    );

    final android = local.resolvePlatformSpecificImplementation<
        AndroidFlutterLocalNotificationsPlugin>();
    if (android == null) return;

    // High importance: a dispute or a payout is exactly the notification a user
    // wants to interrupt for. Without HIGH the message is silently downgraded to
    // a doze-delayed trickle.
    await android.createNotificationChannel(
      const AndroidNotificationChannel(
        _channelId,
        _channelName,
        description: _channelDescription,
        importance: Importance.high,
      ),
    );
  }

  Future<void> _createChannel() async {
    await ensureChannel();

    if (!Platform.isAndroid) return;
    final android = _local.resolvePlatformSpecificImplementation<
        AndroidFlutterLocalNotificationsPlugin>();
    if (android == null) return;

    // Asked here rather than in the shared helper because the background isolate
    // has no business prompting the user for anything.
    await android.requestNotificationsPermission();
  }

  void _wireCallbacks() {
    // Guarded, because `initPush` is re-run when the user switches push on in
    // Notification Settings. `FirebaseMessaging.onMessage.listen` appends to a
    // broadcast stream rather than replacing, so without this the second run
    // doubles every handler: the foreground hook fires twice, and a tray tap
    // navigates twice (pushing the same route onto the stack, so Back appears to
    // do nothing).
    if (_callbacksWired) return;
    _callbacksWired = true;

    // Foreground: the WebSocket usually got here first, and the SnackBar in
    // `NotificationRealtimeBinder` covers the in-app case. Showing a system
    // notification as well would double-notify while the user is looking at the
    // screen, so this is deliberately silent -- it only refreshes the badge.
    FirebaseMessaging.onMessage.listen(_onForegroundMessage);

    // Tapped from the tray: the app was killed, so this is the only signal and
    // the navigation has to happen now.
    //
    // Note this is *not* routed through `NotificationResponse`. That type belongs
    // to `flutter_local_notifications` and carries the payload string this app
    // passed to `show()` -- but this app never shows a local notification, since
    // Android renders the `notification` block FCM already sent. The FCM data map
    // is therefore handled directly.
    FirebaseMessaging.onMessageOpenedApp.listen((message) {
      _onTapPayload(message.data);
    });

    FirebaseMessaging.instance.onTokenRefresh.listen((token) {
      // FCM rotates the token on reinstall, on restore, and periodically. Without
      // this the server keeps pushing to a token the device no longer owns.
      unawaited(_registerToken(token));
    });
  }

  void _onForegroundMessage(RemoteMessage message) {
    final data = message.data;
    debugPrint('[push] foreground ${data['type']}');
    final badge = int.tryParse(data['notification_id'] ?? '');
    // Nothing to do beyond logging here; the socket binder owns the badge and
    // the SnackBar so a push and a socket push cannot both increment it.
    if (badge != null) debugPrint('[push] notification id $badge');
  }

  /// Handles a tap on a tray notification.
  ///
  /// Navigating straight from here is unreliable: when the app was killed this
  /// runs before any route exists, and `navigatorKey.currentState` is null. The
  /// payload is therefore stashed and replayed by the shell once the navigator is
  /// up.
  void _onTapPayload(Map<String, dynamic> data) {
    // Narrowed once here rather than sprinkling `.toString()` through the routing
    // code. FCM also injects `_click_action`; it is dropped so the app's own
    // `route` key stays the single source of truth for navigation.
    final payload = <String, String>{
      for (final entry in data.entries)
        if (entry.key != '_click_action' && entry.value != null)
          entry.key: entry.value.toString(),
    };

    final route = payload['route'];
    debugPrint('[push] tapped, route=$route');
    if (route == null || route.isEmpty) return;
    PendingNotificationTap.enqueue(route, payload);
  }

  Future<void> _registerToken(String token) async {
    _token = token;

    // Do not POST without a session. The endpoint is `[Authorize]`, so a signed
    // out cold start would get a 401, and the shared network layer treats that as
    // an expired session: it tries a token refresh, finds nothing, and redirects
    // to sign-in with a "Session expired" toast. On a fresh install -- before the
    // user has ever signed in -- that is the app greeting them with an auth error
    // and a navigation it did not ask for. The token is kept in memory and
    // registered on the next sign-in instead.
    if (getToken.isEmpty) {
      debugPrint('[push] no session yet; deferring device registration');
      return;
    }

    try {
      // Goes through the shared network layer so the bearer token, the 401
      // refresh, and the error toasts all behave exactly as they do for every
      // other authenticated write. The response is discarded: a registration that
      // fails is retried on the next cold start, so surfacing it would be noise.
      await NetworkApiServices().postApi(
        {'token': token, 'platform': 'android'},
        AppUrls.registerDeviceUrl,
        LocalKeys.pushNotifications,
        headers: acceptJsonAuthHeader,
      );
      notifyListeners();
    } catch (e) {
      debugPrint('[push] token registration failed: $e');
    }
  }
}

/// A tap that arrived before the navigator existed.
class PendingNotificationTap {
  static String? _route;
  static Map<String, String> _data = const {};

  static void enqueue(String route, Map<String, String> data) {
    _route = route;
    _data = data;
  }

  /// Returns the pending tap and clears it, so it is handled exactly once.
  static ({String route, Map<String, String> data})? take() {
    final route = _route;
    if (route == null) return null;
    _route = null;
    final data = _data;
    _data = const {};
    return (route: route, data: data);
  }
}
