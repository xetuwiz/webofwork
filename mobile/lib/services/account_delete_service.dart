import 'package:flutter/material.dart';

import '../../data/network/network_api_services.dart';
import '../../helper/app_urls.dart';
// For acceptJsonAuthHeader. The Apple branch that also needed sPref from here is
// gone, but the header constant is still what authenticates this request.
import '../../helper/constant_helper.dart';
import '../../helper/local_keys.g.dart';

/// Deletes the account on the server.
///
/// Scope note: the Apple token-revocation branch this used to carry has been
/// removed, along with the `clientSecret` constant it depended on.
///
/// It was unreachable and should not have been there. It fired only when
/// `apple_user_id` was present in shared preferences, and nothing in the app
/// ever writes that key — there is no Apple sign-in, and the brief scopes this
/// app to freelancer registration by email and OTP only. So the branch was dead
/// code that could never run, and it was dead in the worst way:
///
///   * it `debugPrint`ed a full URL containing a bearer token and the client
///     secret, outside any `kDebugMode` guard, unlike every other network-layer
///     log in the app;
///   * it put both in a query string, so they would also have landed in any
///     proxy or access log between the device and Apple.
///
/// Keeping a code path for a capability the app does not have is not free. It
/// carries a secret, it implies a feature that isn't there, and it has to be
/// reviewed on every change. If Apple sign-in is ever added, revocation belongs
/// in that feature — with the client secret from a build-time define and the
/// token in a form body, not a URL.
class AccountDeleteService with ChangeNotifier {
  Future tryAccountDelete({password}) async {
    final responseData = await NetworkApiServices().postApi(
        {}, AppUrls.deleteAccountUrl, LocalKeys.deleteAccount,
        headers: acceptJsonAuthHeader);

    if (responseData != null) {
      return true;
    }
  }
}