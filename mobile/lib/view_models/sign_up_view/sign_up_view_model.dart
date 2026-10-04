import 'package:flutter/material.dart';
import 'package:provider/provider.dart';
import 'package:webofwork/helper/extension/context_extension.dart';
import 'package:webofwork/helper/extension/string_extension.dart';
import 'package:webofwork/services/auth/sign_up_service.dart';
import 'package:webofwork/views/sign_up_view/components/verify_otp_view.dart';

import '../../helper/local_keys.g.dart';

import '../../services/profile_info_service.dart';

/// Drives the two-step registration form.
///
/// Step 1 posts `{full_name, email, password}` to `/api/auth/send-otp`; step 2 is
/// the OTP screen, which posts `{email, otp_code}` to `/api/auth/verify-otp` and
/// is the only place the session is issued.
///
/// The previous version collected first name, last name, username, email, phone
/// and password, posted them to `/api/auth/register/` -- a route that does not
/// exist on this API -- and then compared the OTP the user typed against
/// `su.emailToken` **in Dart** before calling a verify endpoint that expected a
/// different body. `username` and `phone` are not part of registration here, and
/// `full_name` is capped at 40 characters server-side.
class SignUpViewModel {
  /// Owned by the view's State, not by this singleton.
  GlobalKey<FormState>? formKey;

  final ValueNotifier<bool> obscurePassNew = ValueNotifier<bool>(true);
  final ValueNotifier<bool> obscurePassConfirm = ValueNotifier<bool>(true);
  final ValueNotifier<bool> loading = ValueNotifier<bool>(false);
  final ValueNotifier<bool> tacPpAccept = ValueNotifier(false);

  final TextEditingController fullNameController = TextEditingController();
  final TextEditingController emailController = TextEditingController();
  final TextEditingController passwordController = TextEditingController();
  final TextEditingController confirmPasswordController = TextEditingController();

  SignUpViewModel._init();
  static SignUpViewModel? _instance;
  static SignUpViewModel get instance {
    _instance ??= SignUpViewModel._init();
    return _instance!;
  }

  static get dispose {
    _instance = null;
    return true;
  }

  passwordValidator(BuildContext context, value) {
    if (value == null || value.isEmpty || value.length < 6) {
      return LocalKeys.passwordMinLength;
    }
    return null;
  }

  /// `AuthController.SendOtp:33` rejects a blank or >40-character `full_name`.
  /// Matching that here keeps the round-trip to the server for cases the app
  /// can already answer, and keeps the two error surfaces from disagreeing.
  static String? fullNameValidator(String? value) {
    final trimmed = value?.trim() ?? '';
    if (trimmed.isEmpty) return LocalKeys.enterFullName;
    if (trimmed.length > 40) return LocalKeys.fullName;
    return null;
  }

  /// Step 1. On success, push the OTP screen; the session is minted there.
  Future<void> signUp(BuildContext context) async {
    if (formKey?.currentState?.validate() == false) return;
    if (!tacPpAccept.value) {
      LocalKeys.pleaseAgreeWitheTacPP.showToast();
      return;
    }

    final service = context.read<SignUpService>();
    final email = emailController.text.trim();
    loading.value = true;
    final sent = await service.sendOtp(
      fullName: fullNameController.text.trim(),
      email: email,
      password: passwordController.text,
    );
    if (!sent) {
      loading.value = false;
      return;
    }

    // The OTP screen owns verification *and* the session. Nothing is persisted
    // yet: a half-registered user with a stored token would be a ghost account,
    // because `/verify-otp` is what actually creates the user row.
    if (!context.mounted) {
      loading.value = false;
      return;
    }
    context.toPage(
      VerifyOtpView(email: email),
      then: (verified) async {
        if (verified != true) {
          loading.value = false;
          return;
        }
        // No device token to register: the notification socket is opened by
        // `NotificationRealtimeBinder` when the shell mounts. Pull the profile
        // now so the shell has a populated `ProfileInfoService` on first frame.
        await context.read<ProfileInfoService>().fetchProfileInfo();
        // Replace the OTP screen and the form with the shell. `popTrue` is a
        // post-frame pop, so both pops are queued here in order.
        context.popTrue;
        context.popTrue;
        loading.value = false;
        dispose;
      },
    );
  }
}
