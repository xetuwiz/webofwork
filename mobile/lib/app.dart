import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:provider/provider.dart';
import 'package:webofwork/main.dart';

import '/views/splash_view/splash_view.dart';
import 'customizations.dart';
import 'helper/constant_helper.dart';
import 'helper/providers.dart';
import 'helper/routes.dart';
import 'services/dynamics/dynamics_service.dart';
import 'services/push_notification_service.dart';
import 'themes/default_themes.dart';

class WebOfWorkApp extends StatelessWidget {
  const WebOfWorkApp({super.key});

  @override
  Widget build(BuildContext context) {
    return MultiProvider(
      providers: Providers.providers,
      child: Consumer<DynamicsService>(
        builder: (context, dProvider, child) {
          print("auto Building");
          coreInit(context);

          // FCM is initialised here rather than in `main()` because the whole
          // point is the *background* handler: it has to be registered in the
          // isolate that Android starts when it delivers a message to a dead app,
          // which means it must be handed to Firebase before any UI is built. Doing
          // it from a widget means the very first cold-start push is dropped.
          //
          // Fire-and-forget and deliberately not awaited: a slow token fetch must
          // not delay the splash screen, and every failure path inside already
          // degrades to "push unavailable" rather than throwing.
          PushBootstrap.ensureStarted(context);

          return FutureBuilder(
              future: dProvider.onceRebuilt ? null : dProvider.getColors(),
              builder: (context, sn) {
                return MaterialApp(
                  navigatorKey: navigatorKey,
                  debugShowCheckedModeBanner: false,
                  localizationsDelegates: const [
                    GlobalMaterialLocalizations.delegate,
                    GlobalWidgetsLocalizations.delegate,
                    GlobalCupertinoLocalizations.delegate,
                  ],
                  supportedLocales: [dProvider.appLocal, const Locale('en', "US")],
                  builder: (context, rtlChild) {
                    return Directionality(
                      textDirection: dProvider.textDirectionRight
                          ? TextDirection.rtl
                          : TextDirection.ltr,
                      child: rtlChild!,
                    );
                  },
                  title: appLabel,
                  theme: DefaultThemes().themeData(context, dProvider),
                  home: child,
                  routes: Routes.routes,
                );
              });
        },
        child: const SplashView(),
      ),
    );
  }
}

/// Starts push exactly once per process.
///
/// `WebOfWorkApp.build` runs on every `DynamicsService` notification, and this
/// app's theme provider notifies often, so without the latch the token would be
/// fetched and POSTed on every rebuild.
class PushBootstrap {
  static bool _started = false;

  static void ensureStarted(BuildContext context) {
    if (_started) return;
    _started = true;

    // The permission prompt is NOT requested here. It is deferred to the moment
    // the user turns on push in Notification Settings, because an unexplained
    // permission dialog on first launch is the fastest way to get a permanent
    // denial -- and a permanent denial can only be undone from Android settings.
    context.read<PushNotificationService>().initPush(requestPermission: false);
  }
}
