import 'package:flutter/material.dart';
// `material.dart` re-exports only a subset of foundation, and `ValueListenable`
// is not in it -- the fields below are typed as the interface, not as the
// concrete `ValueNotifier`, so this has to be imported explicitly.
import 'package:flutter/foundation.dart' show ValueListenable;
import 'package:webofwork/helper/extension/context_extension.dart';
import 'package:webofwork/helper/extension/int_extension.dart';
import 'package:webofwork/helper/local_keys.g.dart';
import 'package:webofwork/utils/components/field_with_label.dart';
import 'package:webofwork/views/payment_settings_view/components/payout_info_card.dart';

/// The form half of the payout screen: the stored address above, the editable
/// field below, and the explanation that tells the user what the address is for.
///
/// Split from the view so the screen owns only the load/save lifecycle.
class PaymentSettingsForm extends StatelessWidget {
  final TextEditingController controller;
  final GlobalKey<FormState> formKey;
  final ValueListenable<bool> saving;
  final ValueListenable<String> storedEmail;
  final VoidCallback onSave;

  const PaymentSettingsForm({
    super.key,
    required this.controller,
    required this.formKey,
    required this.saving,
    required this.storedEmail,
    required this.onSave,
  });

  @override
  Widget build(BuildContext context) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        ValueListenableBuilder<String>(
          valueListenable: storedEmail,
          builder: (context, stored, _) => PayoutInfoCard(
            email: stored.trim().isEmpty ? null : stored.trim(),
          ),
        ),
        24.toHeight,
        Form(
          key: formKey,
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text(LocalKeys.paypalEmail, style: context.titleSmall?.bold6),
              12.toHeight,
              FieldWithLabel(
                label: LocalKeys.paypalEmail,
                hintText: LocalKeys.enterPaypalEmail,
                controller: controller,
                keyboardType: TextInputType.emailAddress,
                textInputAction: TextInputAction.done,
                // The server stores whatever string it is handed, so this is the
                // only place a typo can be caught.
                validator: (value) {
                  final text = (value ?? '').trim();
                  if (text.isEmpty) return LocalKeys.fieldIsRequired;
                  if (!text.contains('@') || !text.contains('.')) {
                    return LocalKeys.enterValidEmail;
                  }
                  return null;
                },
              ),
              24.toHeight,
              ValueListenableBuilder<bool>(
                valueListenable: saving,
                builder: (context, busy, _) => SizedBox(
                  width: double.infinity,
                  child: ElevatedButton(
                    onPressed: busy ? null : onSave,
                    child: Text(LocalKeys.saveChanges),
                  ),
                ),
              ),
            ],
          ),
        ),
        24.toHeight,
        const PayoutExplainer(),
      ],
    );
  }
}
