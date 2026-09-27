import 'package:flutter/material.dart';
import 'package:provider/provider.dart';
import 'package:webofwork/helper/extension/string_extension.dart';
import 'package:webofwork/helper/local_keys.g.dart';
import 'package:webofwork/models/profile_details_model.dart' show PaymentSettings;
import 'package:webofwork/services/payment_settings_service.dart';

/// Form state for the payout settings screen.
///
/// Split out of the view so the screen holds no `bool` fields of its own -- the
/// same arrangement as `ProfileEditViewModel`, which keeps the widget tree
/// declarative and lets the sheet below drive the screen's busy state without
/// reaching into it.
class PaymentSettingsViewModel {
  /// Owned by the view's State, not by this object.
  GlobalKey<FormState>? formKey;

  final ValueNotifier<bool> loading = ValueNotifier<bool>(true);
  final ValueNotifier<bool> saving = ValueNotifier<bool>(false);

  final TextEditingController paypalEmailController = TextEditingController();

  /// The address as the server last reported it, so the screen can tell a real
  /// change from a re-save of the same value and skip a pointless request.
  final ValueNotifier<String> storedEmail = ValueNotifier<String>('');

  /// `null` until the first fetch resolves. Distinct from "loaded and empty" --
  /// the first means the screen should show the skeleton, the second means the
  /// freelancer genuinely has no address yet and needs the empty state.
  final ValueNotifier<bool> loaded = ValueNotifier<bool>(false);

  Future<void> initSettings(BuildContext context) async {
    loading.value = true;
    final service = Provider.of<PaymentSettingsService>(context, listen: false);
    final settings = await service.fetchPaymentSettings();
    _apply(settings);
    loading.value = false;
    loaded.value = true;
  }

  void _apply(PaymentSettings? settings) {
    final email = settings?.paypalEmail?.trim() ?? '';
    storedEmail.value = email;
    // Seeded only from the server, never from what the user is currently typing:
    // re-seeding on every build would fight the field.
    if (paypalEmailController.text.trim() != email) {
      paypalEmailController.text = email;
    }
  }

  /// True when the field differs from the stored address, i.e. the save button
  /// has something real to do.
  bool get isDirty {
    final typed = paypalEmailController.text.trim();
    return typed.isNotEmpty && typed != storedEmail.value;
  }

  /// Validates and writes the address. Returns true when the server stored it.
  Future<bool> tryUpdatePaypalEmail(BuildContext context) async {
    if (saving.value) return false;
    if (!(formKey?.currentState?.validate() ?? false)) return false;

    saving.value = true;
    try {
      final service = Provider.of<PaymentSettingsService>(context, listen: false);
      final ok = await service.tryUpdatePaypalEmail(paypalEmailController.text);
      if (!ok) return false;

      // The saved address is authoritative, so re-read rather than trust the
      // local field: the server trims, and it may normalise the address.
      _apply(await service.fetchPaymentSettings());
      LocalKeys.paymentDetailsUpdated.showToast();
      return true;
    } finally {
      saving.value = false;
    }
  }

  /// The address as typed, or null when it is blank -- which the empty state
  /// renders as "Not set" rather than an empty box.
  String? get displayEmail {
    final value = storedEmail.value.trim();
    return value.isEmpty ? null : value;
  }

  /// Called from the view's `dispose`. Every notifier and controller this object
  /// created is owned by the State, so the State has to release them.
  void dispose() {
    paypalEmailController.dispose();
    loading.dispose();
    saving.dispose();
    storedEmail.dispose();
    loaded.dispose();
  }
}
