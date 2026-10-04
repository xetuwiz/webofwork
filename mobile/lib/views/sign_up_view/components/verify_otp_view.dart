import 'package:flutter/material.dart';
import 'package:lottie/lottie.dart';
import 'package:pinput/pinput.dart';
import 'package:provider/provider.dart';
import 'package:webofwork/helper/extension/context_extension.dart';
import 'package:webofwork/helper/extension/string_extension.dart';
import 'package:webofwork/helper/local_keys.g.dart';
import 'package:webofwork/services/auth/otp_service.dart';
import 'package:webofwork/services/auth/sign_up_service.dart';
import 'package:webofwork/utils/components/custom_button.dart';
import 'package:webofwork/utils/components/custom_preloader.dart';
import 'package:webofwork/utils/components/empty_spacer_helper.dart';
import 'package:webofwork/utils/components/field_label.dart';
import 'package:webofwork/utils/components/navigation_pop_icon.dart';
import 'package:webofwork/view_models/sign_up_view/sign_up_view_model.dart';

/// Step 2 of registration: the emailed 6-digit code.
///
/// This screen **submits** the code to `POST /api/auth/verify-otp` and trusts the
/// answer. The screen it replaces (`views/reset_password/enter_otp_view.dart`)
/// did the opposite: it received a `token` and an `otp` field as constructor
/// arguments and compared the user's keystrokes against them **in Dart**
/// (`if (s != otp && s != ...otpCode) ... reject`). Both of those values came
/// from responses that never carry them -- `verify-otp` returns
/// `{message, user, access, access_token, refresh}` and `resend-otp` returns
/// `{message}` -- so the comparison was against `null`, and any 6 characters the
/// server had not yet checked would have passed the client gate. The server is
/// the only party that can validate a code it emailed.
class VerifyOtpView extends StatefulWidget {
  /// The address step 1 was sent to. The server keys the OTP off the address,
  /// not off a user id -- there is no user yet.
  final String email;

  const VerifyOtpView({super.key, required this.email});

  @override
  State<VerifyOtpView> createState() => _VerifyOtpViewState();
}

class _VerifyOtpViewState extends State<VerifyOtpView> {
  /// The server generates `Random().Next(100000, 999999)` (AuthService.cs:243)
  /// -- always exactly six digits.
  static const int _otpLength = 6;

  final _controller = TextEditingController();
  final _formKey = GlobalKey<FormState>();

  bool _submitting = false;
  String? _error;

  @override
  void initState() {
    super.initState();
    // Start the resend countdown the moment the screen appears: the code was
    // just sent, so the user must not be able to hammer `resend-otp`.
    WidgetsBinding.instance.addPostFrameCallback((_) {
      if (mounted) context.read<OtpService>().markSent();
    });
  }

  @override
  void dispose() {
    _controller.dispose();
    super.dispose();
  }

  Future<void> _submit() async {
    if (_submitting) return;
    final code = _controller.text.trim();
    if (code.length != _otpLength) {
      setState(() => _error = LocalKeys.enterYourVerificationCode);
      return;
    }

    setState(() {
      _submitting = true;
      _error = null;
    });

    final signUp = context.read<SignUpService>();
    final verified = await signUp.verifyOtp(email: widget.email, otpCode: code);

    if (!mounted) return;

    if (!verified) {
      // `verifyOtp` has already toasted the server's own message ("Invalid OTP",
      // or "Registration session expired. Please register again."). Clear the
      // field so the next attempt starts from a blank box, and keep the screen
      // up so a typo does not cost the user their place in the flow.
      _controller.clear();
      setState(() {
        _submitting = false;
        _error = LocalKeys.wrongOTPCode;
      });
      return;
    }

    // The account exists but is a Client. Promote it, otherwise every
    // freelancer route 403s the moment the shell mounts.
    await signUp.activateAsFreelancer();

    if (!mounted) return;
    // Hand control back to `SignUpViewModel.signUp`, which pops both this screen
    // and the form, then loads the profile.
    context.popTrue;
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(
        leading: NavigationPopIcon(
          onTap: () {
            // Undo the countdown so re-entering from the form starts fresh.
            context.read<OtpService>().reset();
            SignUpViewModel.instance.loading.value = false;
            context.popFalse;
          },
        ),
        title: Text(LocalKeys.verificationCode.capitalizeWords),
      ),
      body: SingleChildScrollView(
        padding: const EdgeInsets.all(20),
        child: Container(
          padding: const EdgeInsets.all(20),
          decoration: BoxDecoration(
            color: context.dProvider.whiteColor,
            borderRadius: BorderRadius.circular(8),
          ),
          child: Form(
            key: _formKey,
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                LottieBuilder.asset(
                  'assets/animations/otp_animation.json',
                  repeat: false,
                ),
                FieldLabel(label: LocalKeys.enterYourVerificationCode),
                Text(
                  LocalKeys.doNotShareCode,
                  style: Theme.of(context)
                      .textTheme
                      .titleSmall
                      ?.copyWith(color: context.dProvider.black6),
                ),
                EmptySpaceHelper.emptyHeight(20),
                Center(child: _buildPinput(context)),
                if (_error != null) ...[
                  EmptySpaceHelper.emptyHeight(12),
                  Center(
                    child: Text(
                      _error!,
                      style: context.titleSmall
                          ?.copyWith(color: context.dProvider.warningColor),
                    ),
                  ),
                ],
                EmptySpaceHelper.emptyHeight(30),
                SizedBox(
                  height: 40,
                  child: Center(child: _buildResendRow(context)),
                ),
                EmptySpaceHelper.emptyHeight(24),
                CustomButton(
                  onPressed: _submitting ? () {} : _submit,
                  btText: LocalKeys.verify,
                  isLoading: _submitting,
                ),
              ],
            ),
          ),
        ),
      ),
    );
  }

  Widget _buildPinput(BuildContext context) {
    final base = PinTheme(
      width: 44,
      height: 52,
      textStyle: TextStyle(fontSize: 18, color: context.dProvider.black4),
      decoration: BoxDecoration(
        border: Border.all(color: context.dProvider.black8),
        borderRadius: BorderRadius.circular(8),
      ),
    );
    return Pinput(
      controller: _controller,
      length: _otpLength,
      autofocus: true,
      keyboardType: TextInputType.number,
      onCompleted: (_) => _submit(),
      defaultPinTheme: base,
      focusedPinTheme: base.copyDecorationWith(
        border: Border.all(color: context.dProvider.primaryColor),
        borderRadius: BorderRadius.circular(8),
      ),
      // Pinput's own `validator` is for form validation; the server decides
      // whether a code is right, so nothing is asserted here beyond the field
      // being complete.
      errorText: _error,
      pinContentAlignment: Alignment.center,
    );
  }

  Widget _buildResendRow(BuildContext context) {
    return Consumer<OtpService>(
      builder: (context, otp, _) {
        final seconds = otp.secondsRemaining;
        final enabled = otp.canResend;

        if (otp.loadingSendOTP) {
          return const SizedBox(
            height: 24,
            child: FittedBox(child: CustomPreloader()),
          );
        }

        return Row(
          mainAxisAlignment: MainAxisAlignment.center,
          children: [
            Text(
              seconds > 0
                  ? '${LocalKeys.sendAgainIn} $seconds${LocalKeys.seconds}'
                  : LocalKeys.didNotReceiveCode,
              style: TextStyle(
                color: context.dProvider.black5,
                fontWeight: FontWeight.w600,
              ),
            ),
            EmptySpaceHelper.emptyWidth(6),
            // A TextButton, not a tappable RichText: the old link fired even
            // while the cooldown was running, so the countdown was decorative.
            TextButton(
              onPressed: enabled
                  ? () async {
                      await otp.resend(widget.email);
                      if (mounted) _controller.clear();
                    }
                  : null,
              style: TextButton.styleFrom(
                padding: EdgeInsets.zero,
                minimumSize: const Size(0, 0),
                tapTargetSize: MaterialTapTargetSize.shrinkWrap,
                foregroundColor: context.dProvider.primaryColor,
                disabledForegroundColor: context.dProvider.black8,
              ),
              child: Text(
                // The template already ships this string as `resendCode`; adding
                // a second key with the same value would collide in
                // `LocalKeys.stringsMap`.
                LocalKeys.resendCode,
                style: context.titleSmall?.bold6.copyWith(
                  color:
                      enabled ? context.dProvider.primaryColor : context.dProvider.black8,
                ),
              ),
            ),
          ],
        );
      },
    );
  }
}
