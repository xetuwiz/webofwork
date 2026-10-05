import 'package:flutter/material.dart';
import 'package:provider/provider.dart';
import 'package:webofwork/services/profile_info_service.dart';
import 'package:webofwork/services/push_notification_service.dart';

import '/helper/extension/context_extension.dart';
import '../../data/network/network_api_services.dart';
import '../../helper/constant_helper.dart';
import '../../services/auth/sign_in_service.dart';

import '../../views/onboarding_view/onboarding_view.dart';

class SignInViewModel {
  /// Owned by the view's State, not by this singleton. A GlobalKey held by a
  /// singleton collides as soon as the view is mounted twice in the tree.
  GlobalKey<FormState>? formKey;
  final ValueNotifier obscurePass = ValueNotifier<bool>(true);
  final ValueNotifier rememberPass = ValueNotifier<bool>(true);
  final ValueNotifier loading = ValueNotifier<bool>(false);
  final TextEditingController emailUsernameController = TextEditingController();
  final TextEditingController passwordController = TextEditingController();

  SignInViewModel._init();
  static SignInViewModel? _instance;
  static SignInViewModel get instance {
    _instance ??= SignInViewModel._init();
    return _instance!;
  }

  static get dispose {
    _instance = null;
    return true;
  }

  passwordValidator(BuildContext context, value) {
    if (value == null || value.isEmpty || value.trim().isEmpty || value.length < 6) {
      return 'Password must be at least 6 characters';
    }
    return null;
  }

  emailValidator(value) {
    if (value == null || value.isEmpty || value.trim().isEmpty) {
      return 'Please enter your email address';
    }
    return null;
  }

  /// Persist email for "remember me" convenience (no password stored).
  _saveEmail(String email) {
    sPref?.setString('user-email', email);
    sPref?.setBool('user-remember', rememberPass.value);
  }

  initSavedInfo() {
    emailUsernameController.text = sPref?.getString('user-email') ?? '';
    rememberPass.value = sPref?.getBool('user-remember') ?? true;
  }

  signIn(BuildContext context) async {
    final isValid = formKey?.currentState?.validate();
    if (isValid == false) return;

    loading.value = true;

    final siProvider = Provider.of<SignInService>(context, listen: false);
    final piProvider = Provider.of<ProfileInfoService>(context, listen: false);

    final result = await siProvider.trySignIn(
      emailUsername: emailUsernameController.text.trim(),
      password: passwordController.text,
    );

    if (result == true) {
      if (rememberPass.value) {
        _saveEmail(emailUsernameController.text.trim());
      }
      
      final push = Provider.of<PushNotificationService>(context, listen: false);
      await push.reregister();

      await piProvider.fetchProfileInfo();
      loading.value = false;
      dispose;
      // Sign-in is reachable two ways: pushed on top of the shell, or installed
      // as the *only* route by the 401 redirect. `pop` only works in the first
      // case, so replace the stack with the shell in both — popping the sole
      // route is what left the user staring at a black screen.
      NetworkApiServices.resetAuthRedirectGuard();
      context.toUntilPage(const OnboardingView());
    } else {
      loading.value = false;
    }
  }
}
