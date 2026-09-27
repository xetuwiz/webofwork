import 'package:flutter/foundation.dart';
import 'package:webofwork/data/network/network_api_services.dart';
import 'package:webofwork/helper/app_urls.dart';
import 'package:webofwork/helper/constant_helper.dart';
import 'package:webofwork/helper/local_keys.g.dart';
import 'package:webofwork/models/notification_preferences_model.dart';

/// Reads and writes the user's notification channel switches and the
/// per-type, per-channel mute grid.
///
/// ## What changed
///
/// This used to handle three channels and one flat `muted_types` list, with a
/// comment explaining that there was deliberately no email switch because the
/// server only wired its SMTP sender to OTP and password reset -- "such a toggle
/// would look real and do nothing". Email is now dispatched by
/// `NotificationService`, so the switch is honest and this service sends it.
///
/// The mute shape changed with it: `muted` is now a list of `{type, channel}`
/// pairs. The server treats it as a replacement, so a cell toggle recomputes the
/// whole list; see `NotificationPreferences.toggle`.
class NotificationPreferencesService with ChangeNotifier {
  Future<NotificationPreferences?> fetchPreferences() async {
    final response = await NetworkApiServices().getApi(
      AppUrls.notificationPreferencesUrl,
      LocalKeys.notificationPreferences,
      headers: acceptJsonAuthHeader,
    );
    // Wrapped in `data` by the controller.
    final body = (response is Map) ? response['data'] : null;
    if (body is! Map) return null;
    return NotificationPreferences.fromJson(Map<String, dynamic>.from(body));
  }

  /// The server's channel + type vocabulary, cached after the first successful
  /// call.
  ///
  /// Not needed to render the switches any more -- the preferences response now
  /// carries `channels` and `types` -- but kept because the settings screen falls
  /// back to it when the preferences request fails, so the user still sees which
  /// events exist rather than an empty page.
  List<NotificationTypeOption>? _typeVocabulary;
  List<NotificationTypeOption> get typeVocabulary => _typeVocabulary ?? const [];

  Future<void> loadTypeVocabulary() async {
    final response = await NetworkApiServices().getApi(
      AppUrls.notificationPreferenceTypesUrl,
      LocalKeys.notificationPreferences,
      headers: acceptJsonAuthHeader,
    );
    final body = (response is Map) ? response['data'] : null;
    if (body is Map) {
      final types = (body['types'] as List?) ?? const [];
      _typeVocabulary = types
          .map((e) => NotificationTypeOption.fromJson(
              Map<String, dynamic>.from(e as Map)))
          .toList();
      notifyListeners();
    }
  }

  /// Applies a partial update. Only the non-null arguments are sent, so a single
  /// switch can be flipped without resending the whole object.
  ///
  /// [muted] replaces the server's set rather than adding to it, which is why a
  /// cell toggle must pass the complete list.
  ///
  /// Returns the stored state, or null when the write was rejected.
  Future<NotificationPreferences?> updatePreferences({
    bool? masterEnabled,
    bool? inAppEnabled,
    bool? pushEnabled,
    bool? smsEnabled,
    bool? emailEnabled,
    List<NotificationMute>? muted,
  }) async {
    final response = await NetworkApiServices().putApi(
      {
        if (masterEnabled != null) 'master_enabled': masterEnabled,
        if (inAppEnabled != null) 'in_app_enabled': inAppEnabled,
        if (pushEnabled != null) 'push_enabled': pushEnabled,
        if (smsEnabled != null) 'sms_enabled': smsEnabled,
        if (emailEnabled != null) 'email_enabled': emailEnabled,
        if (muted != null) 'muted': muted.map((m) => m.toJson()).toList(),
      },
      AppUrls.notificationPreferencesUrl,
      LocalKeys.notificationPreferences,
      headers: acceptJsonAuthHeader,
    );

    final body = (response is Map) ? response['data'] : null;
    if (body is! Map) return null;
    final preferences = NotificationPreferences.fromJson(Map<String, dynamic>.from(body));
    notifyListeners();
    return preferences;
  }
}
