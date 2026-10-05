import 'package:flutter/material.dart';
import 'package:webofwork/helper/extension/context_extension.dart';
import 'package:webofwork/helper/local_keys.g.dart';
import 'package:webofwork/utils/components/custom_refresh_indicator.dart';
import 'package:webofwork/utils/components/navigation_pop_icon.dart';
import 'package:webofwork/view_models/payment_settings_view_model/payment_settings_view_model.dart';
import 'package:webofwork/views/payment_settings_view/components/payment_settings_form.dart';

/// The payout destination screen.
///
/// Reachable from Profile Settings. The freelancer could see and change this
/// address on the web client, but this app had no screen for it at all -- and the
/// routes are guarded by `RequireFreelancer`, so the web client was the only
/// place it could be done.
class PaymentSettingsView extends StatefulWidget {
  static const routeName = 'payment_settings_view';
  const PaymentSettingsView({super.key});

  @override
  State<PaymentSettingsView> createState() => _PaymentSettingsViewState();
}

class _PaymentSettingsViewState extends State<PaymentSettingsView> {
  /// Per-view, not a singleton: the form holds a controller seeded from the
  /// current address, so sharing one instance across visits would leak the
  /// previous account's value into the field.
  final PaymentSettingsViewModel psv = PaymentSettingsViewModel();

  @override
  void initState() {
    super.initState();
    psv.formKey = GlobalKey<FormState>();
    // Post-frame: this resolves a provider, which is not safe to do during
    // `initState` when the view is the first descendant to need it.
    WidgetsBinding.instance.addPostFrameCallback((_) => psv.initSettings(context));
  }

  @override
  void dispose() {
    psv.dispose();
    super.dispose();
  }

  Future<void> _save() async {
    final ok = await psv.tryUpdatePaypalEmail(context);
    if (ok && context.mounted) context.popTrue;
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(
        leading: const NavigationPopIcon(),
        title: Text(LocalKeys.paymentMethods),
      ),
      body: ValueListenableBuilder<bool>(
        valueListenable: psv.loaded,
        builder: (context, loaded, _) {
          // Pull-to-refresh has to work even before the first load resolves, or
          // a failed load leaves no way to retry short of leaving the screen.
          if (!loaded && psv.loading.value) {
            return Center(
              child: CircularProgressIndicator(
                color: context.dProvider.primaryColor,
              ),
            );
          }
          return CustomRefreshIndicator(
            onRefresh: () => psv.initSettings(context),
            child: ListView(
              physics: const AlwaysScrollableScrollPhysics(),
              padding: const EdgeInsets.all(20),
              children: [
                PaymentSettingsForm(
                  controller: psv.paypalEmailController,
                  formKey: psv.formKey!,
                  saving: psv.saving,
                  storedEmail: psv.storedEmail,
                  onSave: _save,
                ),
              ],
            ),
          );
        },
      ),
    );
  }
}
