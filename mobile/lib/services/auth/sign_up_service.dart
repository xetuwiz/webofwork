import 'package:flutter/material.dart';
import 'package:webofwork/helper/extension/string_extension.dart';

import '../../data/network/network_api_services.dart';
import '../../helper/app_urls.dart';
import '../../helper/constant_helper.dart';
import '../../helper/local_keys.g.dart';

/// Registration on this API is a two-request OTP flow, verified entirely by the
/// server:
///
///   1. `POST /api/auth/send-otp`   `{full_name, email, password}`
///      -> caches the pending registration in Redis for 10 minutes and emails a
///         code. There is **no** `username`, **no** `phone` and no separate
///         first/last name -- the previous version sent
///         `{first_name, last_name, username, email, phone, password,
///         confirm_password}` to `/api/auth/register/`, a route that does not
///         exist, so every sign-up 404'd.
///   2. `POST /api/auth/verify-otp` `{email, otp_code}`
///      -> creates the user and returns the session:
///         `{message, user: {id, email, full_name, current_role}, access,
///           access_token, refresh}`.
///
/// Step 2 is the only place a session is issued during registration, so the
/// tokens are persisted from here. The old code read `token` and
/// `user.email_verify_token` off the *step 1* response -- neither field exists
/// there -- and then compared the user's typed code against that `emailToken`
/// **on the client**, so the app could not have accepted a code it never
/// received, and could not have known whether the server accepted one.
class SignUpService with ChangeNotifier {
  /// The email the pending registration belongs to, carried from step 1 to
  /// step 2. The server keys the OTP off the address, not off a user id (there
  /// is no user yet).
  String? pendingEmail;

  /// `current_role` from the verify response. The freelancer app only accepts
  /// `freelancer`; anything else means the account was created for the wrong
  /// side of the marketplace and every `[Authorize(Policy = "RequireFreelancer")]`
  /// route would 403.
  String? role;

  /// Step 1. Returns true when the code is on its way.
  ///
  /// `send-otp` 400s with a per-field map (`{email: ["Email already exists"]}`,
  /// `{password: ["This field must be at least 6 characters."]}`) rather than a
  /// single `error` string, so [describeError] unpacks the first field message
  /// instead of falling back to a generic toast.
  Future<bool> sendOtp({
    required String fullName,
    required String email,
    required String password,
  }) async {
    final responseData = await NetworkApiServices().postApi(
      {
        'full_name': fullName,
        'email': email,
        'password': password,
      },
      AppUrls.registerSendOtpUrl,
      LocalKeys.signUp,
    );

    if (responseData is Map) {
      pendingEmail = email.trim().toLowerCase();
      final message = responseData['message']?.toString();
      (message != null && message.isNotEmpty ? message : LocalKeys.otpSentToYourEmail)
          .showToast();
      return true;
    }

    // A 400 body is still a `Map`, so `is Map` is not the success test: an error
    // envelope would sail through. `message` is the only field a 200 carries.
    if (responseData == null) return false;
    describeError(responseData).showToast();
    return false;
  }

  /// Step 2. Verifies the code **server-side** and persists the session.
  Future<bool> verifyOtp({required String email, required String otpCode}) async {
    final responseData = await NetworkApiServices().postApi(
      {'email': email, 'otp_code': otpCode},
      AppUrls.registerVerifyOtpUrl,
      LocalKeys.emailVerify,
    );

    if (responseData is! Map) {
      // null == network failure, or a 400 whose body was not JSON.
      if (responseData == null) {
        LocalKeys.wrongOTPCode.showToast();
      }
      return false;
    }

    // `access` is the current key; `access_token` is the legacy alias the
    // backend still emits alongside it. Read both so a rolled-back server does
    // not lock everyone out.
    final access = responseData['access']?.toString() ??
        responseData['access_token']?.toString() ??
        '';
    final refresh = responseData['refresh']?.toString() ?? '';
    final user = responseData['user'];

    if (access.isEmpty) {
      // A 400/500 envelope also arrives as a Map, so this is the real failure
      // branch: "Invalid OTP", or "Registration session expired. Please
      // register again."
      describeError(responseData).showToast();
      return false;
    }

    final userId = user is Map ? user['id']?.toString() ?? '' : '';
    final userEmail = user is Map ? user['email']?.toString() ?? '' : '';
    role = user is Map ? user['current_role']?.toString() : null;

    await setToken(access);
    // The refresh token also arrives as an HttpOnly cookie, which `package:http`
    // has no jar for -- so the body is the only copy a native client can keep.
    if (refresh.isNotEmpty) await setRefreshToken(refresh);

    _verifiedUserId = userId;
    _verifiedEmail = userEmail.isEmpty ? email : userEmail;

    LocalKeys.emailVerifiedSuccessfully.showToast();
    pendingEmail = null;
    return true;
  }

  String _verifiedUserId = '';
  String _verifiedEmail = '';
  String get verifiedUserId => _verifiedUserId;
  String get verifiedEmail => _verifiedEmail;

  /// Promotes the just-created account to a freelancer.
  ///
  /// `AuthService.CreateRegistrationUserAsync` creates every public signup as a
  /// **Client** -- the public form belongs to the client web app, and the server
  /// says so in a comment on line 264. This app is freelancer-only, and every
  /// freelancer route is `[Authorize(Policy = "RequireFreelancer")]`, so without
  /// this call the account verifies successfully and then 403s on its own
  /// dashboard. `POST /api/profile/switch-role` is the intended promotion path
  /// and is available to any authenticated user.
  ///
  /// Returns false only when the call itself failed. Note this cannot fail
  /// *after* the account exists, so the caller treats it as best-effort: a
  /// network blip here leaves a client account, and the user can retry from the
  /// shell.
  Future<bool> activateAsFreelancer() async {
    final responseData = await NetworkApiServices().postApi(
      {'role': 'freelancer'},
      AppUrls.switchRoleUrl,
      LocalKeys.signUp,
      headers: commonAuthHeader,
    );
    // 200 with `{message, current_role}`. A `Map` is enough: a 500 here arrives
    // as `{error: ...}`, which is also a Map, so the message is the real test.
    if (responseData is Map && responseData['current_role'] != null) {
      role = responseData['current_role'].toString();
      return true;
    }
    if (responseData == null) return false;
    describeError(responseData).showToast();
    return false;
  }

  /// Re-sends the registration code.
  ///
  /// Also the liveness probe, exactly as in the web client: a 400 carrying
  /// `non_field_errors` means the 10-minute Redis record is gone and the user has
  /// to start over.
  Future<bool> resendOtp({required String email}) async {
    final responseData = await NetworkApiServices().postApi(
      {'email': email},
      AppUrls.resendRegistrationOtpUrl,
      LocalKeys.sendingOtpToMail,
    );

    if (responseData is Map && responseData['message'] != null) {
      responseData['message'].toString().showToast();
      return true;
    }
    if (responseData == null) return false;
    describeError(responseData).showToast();
    return false;
  }

  /// Unpacks this API's two error shapes into one sentence.
  ///
  /// The per-field map (`{email: ["Email already exists"]}`,
  /// `{non_field_errors: ["Registration expired. Please register again."]}`) and
  /// the flat one (`{error: "Invalid OTP"}`) are both in use on these routes, and
  /// the flat shape is nested under `errors` on the 422 path. `returnResponse`
  /// already toasts the common cases, so this is for the callers that want to
  /// render the message inline.
  static String describeError(dynamic body) {
    if (body is String) return body;
    if (body is! Map) return LocalKeys.errorOccurred;

    final flat = body['error']?.toString() ??
        body['message']?.toString() ??
        body['detail']?.toString();
    if (flat != null && flat.trim().isNotEmpty) return flat;

    final errors = body['errors'];
    if (errors is Map && errors.isNotEmpty) {
      for (final key in errors.keys) {
        final value = errors[key];
        if (value is List && value.isNotEmpty) return value.first.toString();
        if (value is String && value.isNotEmpty) return value;
      }
    }

    // `{full_name: [...], email: [...], password: [...]}` straight on the body.
    for (final entry in body.entries) {
      final value = entry.value;
      if (value is List && value.isNotEmpty) return value.first.toString();
    }

    return LocalKeys.errorOccurred;
  }
}
