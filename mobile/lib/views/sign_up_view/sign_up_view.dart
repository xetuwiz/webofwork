import 'package:flutter/material.dart';
import 'package:webofwork/helper/extension/context_extension.dart';
import 'package:webofwork/helper/extension/string_extension.dart';
import 'package:webofwork/utils/components/pass_field_with_label.dart';

import '../../utils/components/field_with_label.dart';
import '../../utils/components/navigation_pop_icon.dart';
import '../../utils/components/tac_pp.dart';
import '../../utils/components/custom_button.dart';
import '../../helper/local_keys.g.dart';
import '../../utils/components/empty_spacer_helper.dart';
import 'components/sign_in_to_account.dart';
import '/view_models/sign_up_view/sign_up_view_model.dart';

/// Step 1 of registration.
///
/// Four fields, because that is what `AuthController.SendOtp` reads:
/// `{full_name, email, password}`. The previous form asked for first name, last
/// name, username, email, phone, password and a confirmation, and posted them to
/// `/api/auth/register/` -- a route this API does not have, so every attempt
/// 404'd. `username` and `phone` are not part of registration here at all, and
/// `full_name` is a single string capped at 40 characters
/// (`AuthController.SendOtp:33`), so the split two-box `NameField` is gone.
///
/// The password confirmation stays: it is a usability affordance, not a wire
/// field, and the server never sees it.
class SignUpView extends StatefulWidget {
  static const routeName = 'sign_up_view';
  const SignUpView({super.key});

  @override
  State<SignUpView> createState() => _SignUpViewState();
}

class _SignUpViewState extends State<SignUpView> {
  // Per-State so two mounted SignUpViews never share one GlobalKey.
  final _formKey = GlobalKey<FormState>();
  final suv = SignUpViewModel.instance;

  @override
  void initState() {
    super.initState();
    suv.formKey = _formKey;
  }

  @override
  void dispose() {
    // The key belongs to this State; leaving it on the singleton would let a
    // disposed Form validate a screen that is gone.
    if (suv.formKey == _formKey) suv.formKey = null;
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(
        leading: const NavigationPopIcon(),
        title: Text(LocalKeys.createAnAccount),
      ),
      body: SingleChildScrollView(
        padding: const EdgeInsets.all(20),
        child: Container(
          padding: const EdgeInsets.all(20),
          decoration: BoxDecoration(
            color: context.dProvider.whiteColor,
            borderRadius: BorderRadius.circular(8),
          ),
          child: AutofillGroup(
            child: Form(
              key: _formKey,
              child: Column(
                children: [
                  FieldWithLabel(
                    label: LocalKeys.fullName,
                    hintText: LocalKeys.enterFullName,
                    controller: suv.fullNameController,
                    autofillHints: const [AutofillHints.name],
                    // Matches the server's own bounds so the round-trip is not
                    // spent on an input the API was always going to reject.
                    validator: SignUpViewModel.fullNameValidator,
                  ),
                  FieldWithLabel(
                    label: LocalKeys.email,
                    hintText: LocalKeys.enterEmail,
                    keyboardType: TextInputType.emailAddress,
                    controller: suv.emailController,
                    autofillHints: const [AutofillHints.email],
                    validator: (value) {
                      if (value == null || !value.trim().validateEmail) {
                        return LocalKeys.enterValidEmailAddress;
                      }
                      return null;
                    },
                  ),
                  PassFieldWithLabel(
                    label: LocalKeys.password,
                    hintText: LocalKeys.enterPassword,
                    autofillHints: const [AutofillHints.newPassword],
                    controller: suv.passwordController,
                    valueListenable: suv.obscurePassNew,
                    validator: (value) => suv.passwordValidator(context, value),
                  ),
                  EmptySpaceHelper.emptyHeight(16),
                  PassFieldWithLabel(
                    label: LocalKeys.confirmPass,
                    valueListenable: suv.obscurePassConfirm,
                    hintText: LocalKeys.enterPassword,
                    validator: (pass) {
                      if (pass != suv.passwordController.text) {
                        return LocalKeys.passwordDidNotMatch;
                      }
                      return null;
                    },
                  ),
                  EmptySpaceHelper.emptyHeight(16),
                  TacPp(valueListenable: suv.tacPpAccept),
                  EmptySpaceHelper.emptyHeight(16),
                  ValueListenableBuilder(
                    valueListenable: suv.loading,
                    builder: (context, loading, child) => CustomButton(
                      onPressed: () => suv.signUp(context),
                      btText: LocalKeys.signUp,
                      isLoading: loading,
                    ),
                  ),
                  EmptySpaceHelper.emptyHeight(24),
                  const SignInToAccount(),
                ],
              ),
            ),
          ),
        ),
      ),
    );
  }
}
