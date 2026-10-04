import 'package:flutter/material.dart';

import '../../data/network/network_api_services.dart';
import '../../helper/app_urls.dart';
import '../../helper/constant_helper.dart';
import '../../helper/extension/string_extension.dart';
import '../../helper/local_keys.g.dart';

class SignInService with ChangeNotifier {
  var token = '';
  var refreshToken = '';
  var email = '';
  var userId = '';

  Future trySignIn({required String emailUsername, required String password}) async {
    final data = {'email': emailUsername, 'password': password};
    final responseData = await NetworkApiServices().postApi(
      data,
      AppUrls.signInUrl,
      LocalKeys.signIn,
    );

    if (responseData != null && responseData.containsKey('access')) {
      LocalKeys.signedInSuccessfully.showToast();

      token = responseData['access']?.toString() ?? '';
      refreshToken = responseData['refresh']?.toString() ?? '';

      final user = responseData['user'];
      email = user is Map ? user['email']?.toString() ?? '' : '';
      userId = user is Map ? user['id']?.toString() ?? '' : '';

      // Persist both tokens. Awaited rather than fired and forgotten: a
      // sign-in that reports success while the keystore write is still in
      // flight can leave the user apparently signed in, then signed out on the
      // next launch.
      await setToken(token);
      await setRefreshToken(refreshToken);

      return true;
    } else if (responseData != null) {
      // Surface API validation errors: { error: "..." } (this backend's shape),
      // { errors: { field: [messages] } } (DRF) or { detail: "..." } (DRF default).
      final detail =
          responseData['detail']?.toString() ?? responseData['error']?.toString();
      if (detail != null) {
        detail.showToast();
        return null;
      }
      final errors = responseData['errors'];
      if (errors is Map && errors.isNotEmpty) {
        final messages = errors.values
            .expand((e) => (e is List) ? e.map((m) => m.toString()) : [e.toString()]);
        messages.join('\n').showToast();
      } else {
        (responseData['title']?.toString() ?? 'Sign in failed').showToast();
      }
    }
    return null;
  }
}
