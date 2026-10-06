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
      final user = responseData['user'];

      // This app is freelancer-only, and the check happens here -- before the
      // tokens are persisted.
      //
      // The ordering is the whole point. Once `setToken` has run the session
      // exists, and a rejection afterwards has to unwind it; a half-unwound
      // session is how a client ends up signed in and staring at freelancer
      // screens hitting freelancer endpoints. So the gate runs against a
      // response that has not been committed yet, and the credentials are simply
      // discarded.
      //
      // `AuthController.Login` already returns `is_freelancer` alongside the
      // tokens, so this costs no extra request. `current_role` is deliberately
      // not used: it reflects `CurrentRole`, which a user can switch, whereas
      // `is_freelancer` is the fact of having a freelancer profile -- the thing
      // the screens in this app actually need.
      if (!(user is Map && user['is_freelancer'] == true)) {
        'This app is for freelancers. Please sign in on the website.'.showToast();
        return null;
      }

      LocalKeys.signedInSuccessfully.showToast();

      token = responseData['access']?.toString() ?? '';
      refreshToken = responseData['refresh']?.toString() ?? '';

      // No `is Map` re-check: the gate above returns unless `user` is a Map, so the
      // analyzer has already narrowed it here.
      email = user['email']?.toString() ?? '';
      userId = user['id']?.toString() ?? '';

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
