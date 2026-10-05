import 'dart:convert';

import 'package:flutter/foundation.dart' show debugPrint, kDebugMode;
import 'package:flutter/widgets.dart' show BuildContext, ChangeNotifier;

import '../helper/constant_helper.dart';
import '../helper/local_keys.g.dart';

/// Locale-aware string lookup.
///
/// **This API has no translation endpoint.** The previous implementation
/// `POST`ed the entire [LocalKeys.stringsMap] to
/// `/api/platform-settings/translations/` and read `strings` back. That route
/// does not exist -- the only platform-settings routes are
/// `GET`/`PATCH /api/admin/settings/global`, which are
/// `[Authorize(Policy = RequireAdmin)]` -- so the request 404'd, and
/// `returnResponse`'s `default` branch toasted the failure on the user's first
/// launch. Nothing was ever translated, and `translatedString` stayed `{}`.
///
/// There is nothing to restore: the English text **is** the `LocalKeys` constants,
/// and [getString] already returns the key's own value when no override exists.
/// So the lookup is local, the remote round trip is gone, and a launch no longer
/// depends on a request that cannot succeed.
class AppStringService with ChangeNotifier {
  /// Overrides, keyed by the raw `LocalKeys` key. Empty unless a caller has
  /// installed one (tests, or a future in-app language picker).
  var translatedString = <String, String>{};

  /// The translated string for [s], or [s] itself when there is no override.
  String getString(String s) {
    final override = translatedString[s];
    if (override != null && override.isNotEmpty) return override;
    return s;
  }

  /// Re-reads any cached overrides and marks the service ready.
  ///
  /// Kept as an `async` step on the start-up sequence
  /// (`SplashViewModel._runStartingSequence`) because it is awaited there, but it
  /// no longer performs I/O: the cache is read synchronously and a miss is not an
  /// error.
  ///
  /// A cached `translated_string` from an older build is still honoured, so an
  /// upgrade does not lose anything a user had already downloaded.
  Future<void> translateStrings(BuildContext context) async {
    if (kDebugMode) {
      debugPrint('Using built-in English strings (no translation endpoint)');
    }

    final cached = sPref?.getString('translated_string');
    if (cached != null && cached.isNotEmpty) {
      try {
        final decoded = jsonDecode(cached);
        if (decoded is Map) {
          translatedString = decoded.map(
            (key, value) => MapEntry(key.toString(), value.toString()),
          );
        }
      } catch (_) {
        // A corrupt cache is not worth failing a launch over; the built-in
        // constants are always available.
        translatedString = {};
      }
    }

    // The language is fixed, so the "has the language changed?" cache key the
    // old code used to decide whether to re-fetch has nothing to compare.
    sPref?.setString('langId', dProvider.languageSlug);

    coreInit(context);
  }
}
