import 'package:firebase_messaging/firebase_messaging.dart';
import 'package:flutter/material.dart';
import 'package:webofwork/app.dart';
import 'package:webofwork/services/push_notification_service.dart';

// Re-exported so `notification_helper.dart`, `network_api_services.dart` and
// `app.dart` keep importing the key from `main.dart` as they always have. The
// declaration itself lives in `helper/constant_helper.dart`, next to the other
// app-wide globals, because a view that needs the navigator key should not have
// to pull in Firebase and the notification plugin to reach it.
export 'helper/constant_helper.dart' show navigatorKey;

void main() async {
  WidgetsFlutterBinding.ensureInitialized();

  // Registered before any UI exists. Android starts a *fresh isolate* to run a
  // message that arrives at a killed app, and it only knows about this handler if
  // it was registered with the Firebase SDK during startup. Setting it after
  // `runApp` loses every push that lands in the first few hundred milliseconds.
  FirebaseMessaging.onBackgroundMessage(firebaseMessagingBackgroundHandler);

  runApp(const WebOfWorkApp());
}
