import 'package:flutter/material.dart';
import 'package:webofwork/data/network/network_api_services.dart';
import 'package:webofwork/helper/app_urls.dart';
import 'package:webofwork/helper/constant_helper.dart';
import 'package:webofwork/helper/extension/context_extension.dart';
import 'package:webofwork/helper/extension/string_extension.dart';
import 'package:webofwork/helper/local_keys.g.dart';
import 'package:webofwork/utils/components/custom_button.dart';
import 'package:webofwork/utils/components/field_with_label.dart';
import 'package:webofwork/utils/components/navigation_pop_icon.dart';

/// "Forgot password" on this API is a **magic link**, not a code.
///
/// `POST /api/auth/forgot-password` always answers 200 with
/// `{message: "Password reset link sent if the email exists."}` -- deliberately,
/// so the endpoint cannot be used to enumerate accounts -- and the user completes
/// the reset by opening a link that carries `?uid=&token=`, which
/// `GET /api/auth/reset-password/validate` and `POST /api/auth/reset-password`
/// consume. A native app cannot receive a URL the user clicks in a browser, so
/// there is deliberately **no** in-app "enter the code" step here.
///
/// The screen this replaces chained `send-otp` (which 400s with
/// `non_field_errors: ["Registration expired. Please register again."]`,
/// because that route only knows about *registration* records), then compared an
/// OTP on the client, then posted `{user_id, otp_code}` to
/// `/api/auth/verify-email/` -- the **email-change** verification route, with a
/// different body. Every leg of that chain was wrong.
class ForgotPasswordView extends StatefulWidget {
  static const routeName = 'forgot_password_view';
  const ForgotPasswordView({super.key});

  @override
  State<ForgotPasswordView> createState() => _ForgotPasswordViewState();
}

class _ForgotPasswordViewState extends State<ForgotPasswordView> {
  final _formKey = GlobalKey<FormState>();
  final _emailController = TextEditingController();
  final _loading = ValueNotifier<bool>(false);

  @override
  void dispose() {
    _emailController.dispose();
    _loading.dispose();
    super.dispose();
  }

  Future<void> _submit() async {
    if (_loading.value) return;
    if (_formKey.currentState?.validate() != true) return;

    _loading.value = true;
    final response = await NetworkApiServices().postApi(
      {'email': _emailController.text.trim()},
      AppUrls.forgotPasswordUrl,
      LocalKeys.forgotPassword,
      headers: commonAuthHeader,
    );
    if (!mounted) return;
    _loading.value = false;

    // The route is deliberately answer-identical for known and unknown
    // addresses, so a 200 is the *only* signal and it must not be read as
    // "that account exists". Show the same confirmation either way.
    if (response is Map && response['message'] != null) {
      context.snackBar(
        LocalKeys.checkYourEmailForResetLink,
        buttonText: LocalKeys.okay,
        duration: const Duration(seconds: 4),
        onTap: () => context.popTrue,
      );
    } else if (response == null) {
      LocalKeys.errorOccurred.showToast();
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(
        leading: const NavigationPopIcon(),
        title: Text(LocalKeys.forgotPassword),
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
                Text(
                  LocalKeys.checkYourEmailForResetLink,
                  style: context.titleSmall?.copyWith(color: context.dProvider.black5),
                ),
                const SizedBox(height: 20),
                FieldWithLabel(
                  label: LocalKeys.email,
                  hintText: LocalKeys.enterEmail,
                  keyboardType: TextInputType.emailAddress,
                  controller: _emailController,
                  autofillHints: const [AutofillHints.email],
                  validator: (value) {
                    if (value == null || !value.trim().validateEmail) {
                      return LocalKeys.enterValidEmailAddress;
                    }
                    return null;
                  },
                ),
                const SizedBox(height: 24),
                ValueListenableBuilder(
                  valueListenable: _loading,
                  builder: (context, loading, child) => CustomButton(
                    onPressed: () => _submit(),
                    btText: LocalKeys.submit,
                    isLoading: loading,
                  ),
                ),
              ],
            ),
          ),
        ),
      ),
    );
  }
}
