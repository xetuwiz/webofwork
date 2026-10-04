import 'package:flutter/material.dart';
import 'package:lottie/lottie.dart';
import 'package:provider/provider.dart';
import 'package:webofwork/helper/constant_helper.dart';
import 'package:webofwork/helper/extension/context_extension.dart';
import 'package:webofwork/helper/local_keys.g.dart';
import 'package:webofwork/services/profile_info_service.dart';
import 'package:webofwork/utils/components/custom_button.dart';

import 'package:webofwork/views/sign_in_view/sign_in_view.dart';

import '../../view_models/sign_in_view/sign_in_view_model.dart';
import '../../view_models/sign_up_view/sign_up_view_model.dart';

/// Shown by the Profile / My Orders screens when `profileInfoModel.data` is
/// null.
///
/// That used to mean "not signed in", so the button always navigated to
/// Sign In — but the data is also null when a *logged-in* user's profile
/// warm-up failed once (the backend was restarting, an AP glitch, a timeout).
/// `getApi` swallows those failures, nothing retried, and the user sat on a
/// "Sign In" button while every other tab worked. Since `data != null` is the
/// only gate, this widget can tell the two cases apart from the token:
///
///  * no token  -> the real sign-in button (unchanged behaviour);
///  * token     -> "Retry": refetches the profile in place. A retry is also
///    fired once on mount, so a tab opened after a transient warm-up failure
///    heals itself without a tap.
class AccountSkeleton extends StatefulWidget {
  const AccountSkeleton({super.key});

  @override
  State<AccountSkeleton> createState() => _AccountSkeletonState();
}

class _AccountSkeletonState extends State<AccountSkeleton> {
  bool _didAttemptAutoRetry = false;

  @override
  void initState() {
    super.initState();
    // Re-fetch once when this skeleton mounts with a token. Guarded so a
    // rebuild that keeps the widget mounted does not hammer the server.
    if (getToken.isNotEmpty) {
      WidgetsBinding.instance.addPostFrameCallback((_) {
        if (!mounted || _didAttemptAutoRetry) return;
        _didAttemptAutoRetry = true;
        context.read<ProfileInfoService>().fetchProfileInfo();
      });
    }
  }

  void _onPressed() {
    if (getToken.isNotEmpty) {
      // Logged in but the profile simply failed to load - retry, don't send
      // an authenticated user back to the sign-in screen.
      context.read<ProfileInfoService>().fetchProfileInfo();
      return;
    }
    SignInViewModel.dispose;
    SignInViewModel.instance.initSavedInfo();
    SignUpViewModel.dispose;
    context.toNamed(SignInView.routeName);
  }

  @override
  Widget build(BuildContext context) {
    return Container(
      // margin: const EdgeInsets.symmetric(vertical: 4),
      alignment: Alignment.center,
      color: context.dProvider.whiteColor,
      child: Column(
        mainAxisAlignment: MainAxisAlignment.center,
        children: [
          LottieBuilder.asset(
            "assets/animations/sign_in.json",
            // width: context.width / 1.7,
            // height: context.width / 1.7,
            fit: BoxFit.cover,
            repeat: false,
          ),
          SizedBox(
            width: context.width / 2,
            child: CustomButton(
              onPressed: _onPressed,
              btText: getToken.isNotEmpty ? LocalKeys.retry : LocalKeys.signIn,
              isLoading: false,
            ),
          )
        ],
      ),
    );
  }
}