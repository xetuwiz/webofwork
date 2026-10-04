import 'package:flutter/material.dart';
import 'package:provider/provider.dart';
import 'package:webofwork/services/push_notification_service.dart';

import '../../data/network/network_api_services.dart';
import '../../helper/app_urls.dart';
import '../../helper/constant_helper.dart';
import '../../helper/extension/string_extension.dart';
import '../../helper/local_keys.g.dart';

class SignOutService with ChangeNotifier {
  trySignOut() async {
    final responseData = await NetworkApiServices()
        .postApi({}, AppUrls.signOutUrl, LocalKeys.signOut, headers: commonAuthHeader);
    if (responseData != null) {
      LocalKeys.signOutComplete.showToast();
      await _releaseDevice();
      await clearTokens();
      return true;
    }
    // Even on network failure, clear local state so the user can re-login.
    await _releaseDevice();
    await clearTokens();
    return true;
  }

  /// Hands this device's FCM token back to the server on the way out.
  ///
  /// Without it the token row stays owned by the account that just signed out, so
  /// on a shared tablet the next person to sign in keeps receiving the previous
  /// user's contract and payment notifications until they happen to trigger a
  /// re-registration. `unregister` swallows its own errors, so a network failure
  /// here can never block the sign-out itself.
  ///
  /// Runs *before* `clearTokens` because the DELETE is `[Authorize]` and needs the
  /// bearer token that is about to be discarded.
  Future<void> _releaseDevice() async {
    try {
      final navigatorContext = navigatorKey.currentContext;
      if (navigatorContext == null) return;
      await navigatorContext.read<PushNotificationService>().unregister();
    } catch (_) {
      // Best effort. Failing to unregister is not a reason to trap the user in a
      // signed-in state.
    }
  }
}
