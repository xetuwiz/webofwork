import 'package:webofwork/helper/extension/string_extension.dart';
import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:provider/provider.dart';
import 'package:webofwork/helper/constant_helper.dart';
import 'package:webofwork/helper/extension/context_extension.dart';
import 'package:webofwork/helper/local_keys.g.dart';
import 'package:webofwork/services/profile_info_service.dart';
import 'package:webofwork/view_models/sign_in_view/sign_in_view_model.dart';
import 'package:webofwork/views/sign_in_view/sign_in_view.dart';
import '../../helper/network_connectivity.dart';
import '../../services/app_string_service.dart';
import '../../views/intro_view/intro_view.dart';
import '../../views/onboarding_view/onboarding_view.dart';
import '/services/intro_service.dart';

class SplashViewModel {
  /// Guards the start-up navigation.
  ///
  /// `initiateStartingSequence` is triggered from `SplashView.build()`, which
  /// re-runs on every rebuild — the root `Consumer` rebuilds while
  /// `getColors()` is still in flight, and the log shows the app building
  /// twice. Because the method is async, the second run started before the
  /// first had finished awaiting, and both would independently push a page.
  static bool _sequenceRunning = false;

  /// [force] is set by the "Retry" button, which must be able to re-run the
  /// sequence after a connectivity failure cleared [ _sequenceRunning ].
  Future initiateStartingSequence(BuildContext context, {bool force = false}) async {
    if (_sequenceRunning && !force) return;
    _sequenceRunning = true;
    try {
      await _runStartingSequence(context);
    } finally {
      _sequenceRunning = false;
    }
  }

  Future _runStartingSequence(BuildContext context) async {
    await coreInit(context);

    // Before anything reads a token. Token reads are synchronous and served from
    // the store's cache, so an un-hydrated cache reads as "signed out" -- which
    // would send a signed-in user to the sign-in screen on every cold start.
    await initiateToken();

    final NetworkConnectivity networkConnectivity = NetworkConnectivity.instance;
    final hasConnection = await networkConnectivity.currentStatus();
    if (!hasConnection) {
      dProvider.setNoConnection(true);
      LocalKeys.noConnectionFound.showToast();
      return;
    }

    SystemChrome.setSystemUIOverlayStyle(
      const SystemUiOverlayStyle(
          statusBarColor: Colors.transparent, statusBarIconBrightness: Brightness.dark),
    );

    await Provider.of<AppStringService>(context, listen: false).translateStrings(context);

    final gotoIntro =
        await Provider.of<IntroService>(context, listen: false).checkIntro();

    if (gotoIntro) {
      await Provider.of<IntroService>(context, listen: false).fetchIntro(context);
      context.toUntilPage(const IntroView());
      return;
    } else if (getToken.isEmpty) {
      SignInViewModel.instance.initSavedInfo();
      context.toUntilPage(const SignInView());
    } else {
      // Token present — go to the shell immediately. The shell's tabs each
      // load their own data from initState, so the profile warm-up here is
      // purely best-effort; chaining navigation to it stranded the splash
      // spinner for the full timeout whenever the server was briefly
      // unreachable (`getApi` swallows the failure and returns null, so the
      // `.then` below never ran).
      Provider.of<ProfileInfoService>(context, listen: false)
          .fetchProfileInfo();
      context.toUntilPage(const OnboardingView());
    }
  }
}
