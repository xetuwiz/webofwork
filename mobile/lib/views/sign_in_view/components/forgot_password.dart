import 'package:flutter/material.dart';

import '/helper/extension/context_extension.dart';
import '/helper/local_keys.g.dart';
import '/views/forgot_password_view/forgot_password_view.dart';

class ForgotPassword extends StatelessWidget {
  const ForgotPassword({super.key});

  @override
  Widget build(BuildContext context) {
    return FittedBox(
      child: ConstrainedBox(
        constraints: BoxConstraints(maxWidth: context.width / 2),
        child: TextButton(
          onPressed: () {
            FocusScope.of(context).unfocus();
            context.toPage(const ForgotPasswordView());
          },
          child: Text(
            LocalKeys.forgotPassword,
            style: context.titleMedium!.copyWith(
              color: context.dProvider.secondaryColor,
            ),
          ),
        ),
      ),
    );
  }
}
