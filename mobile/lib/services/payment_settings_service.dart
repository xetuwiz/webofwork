import 'package:flutter/foundation.dart';
import 'package:webofwork/data/network/network_api_services.dart';
import 'package:webofwork/helper/app_urls.dart';
import 'package:webofwork/helper/constant_helper.dart';
import 'package:webofwork/helper/local_keys.g.dart';
// `PaymentSettings` already exists here (it is part of the profile payload) --
// the payment screen reuses it rather than defining a second, subtly different
// copy of the same two fields.
import 'package:webofwork/models/profile_details_model.dart' show PaymentSettings;

/// Reads and writes the freelancer's payout destination.
///
/// This is a freelancer-only area: both routes are `[Authorize(Policy =
/// "RequireFreelancer")]`, and the whole app is freelancer-only, so there is no
/// client-role branch to hide.
///
/// The server performs **no** validation on the address -- `UpdatePaymentSettingsAsync`
/// assigns whatever string it is handed straight onto the row, so an empty or
/// malformed value is stored happily and the freelancer only finds out when a
/// payout fails. That validation has to live on the form, which is why
/// [tryUpdatePaypalEmail] rejects a blank or malformed address *before* the
/// request rather than after.
class PaymentSettingsService with ChangeNotifier {
  /// Loads the stored PayPal address, or null when the request failed (the
  /// network layer has already surfaced the reason).
  ///
  /// The route answers `Ok(settings ?? new FreelancerPaymentSettings())`, so a
  /// freelancer who has never set one gets a 200 with `paypal_email: null` --
  /// a successful response that legitimately carries no address.
  Future<PaymentSettings?> fetchPaymentSettings() async {
    final response = await NetworkApiServices().getApi(
      AppUrls.paymentSettingsUrl,
      LocalKeys.paymentMethods,
      headers: acceptJsonAuthHeader,
    );

    if (response is! Map) return null;
    // `returnResponse` hands back a `jsonDecode` result, which is
    // `Map<String, dynamic>` in practice but typed `Map<dynamic, dynamic>`.
    return PaymentSettings.fromJson(Map<String, dynamic>.from(response));
  }

  /// Stores [email] as the payout destination. Returns true only when the server
  /// actually accepted it.
  ///
  /// Body is `{"paypal_email": "..."}` -- `PaymentSettingsRequest` is a record
  /// with a single named field, so a bare JSON string (which this route used to
  /// take) binds nothing and silently stores an empty address.
  Future<bool> tryUpdatePaypalEmail(String email) async {
    final trimmed = email.trim();
    if (trimmed.isEmpty || !_looksLikeEmail(trimmed)) return false;

    final response = await NetworkApiServices().putApi(
      {'paypal_email': trimmed},
      AppUrls.paymentSettingsUrl,
      LocalKeys.paymentMethods,
      headers: acceptJsonAuthHeader,
    );

    if (response is! Map) return false;
    // `Ok(new { paypal_email, is_verified })` on success. A 4xx comes back as
    // problem-details or a validation map; the network layer has already toasted
    // it, so this only has to reject.
    return !response.containsKey('errors') &&
        !response.containsKey('detail') &&
        !response.containsKey('title');
  }

  /// Deliberately permissive: one `@`, a dot in the domain, no spaces. The point
  /// is to catch typos, not to re-implement RFC 5322 -- an over-strict regex
  /// here rejects legitimate addresses, which is worse than storing a bad one
  /// the user can then correct.
  static bool _looksLikeEmail(String value) {
    final at = value.indexOf('@');
    if (at <= 0 || at != value.lastIndexOf('@')) return false;
    if (value.contains(' ')) return false;
    final domain = value.substring(at + 1);
    if (!domain.contains('.')) return false;
    if (domain.startsWith('.') || domain.endsWith('.')) return false;
    return true;
  }
}
