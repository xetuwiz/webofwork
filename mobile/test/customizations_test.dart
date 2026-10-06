import 'package:flutter_test/flutter_test.dart';

import 'package:webofwork/customizations.dart';

/// The release endpoint is the one setting that decides whether a shipped APK
/// can talk to anything at all, and nothing else in the toolchain notices when
/// it is wrong.
///
/// `flutter analyze` does not care about the value of a `String.fromEnvironment`
/// default. `flutter test` runs in debug mode, where `kReleaseMode` is false and
/// the https check in `customizations.dart` is skipped entirely. So the previous
/// default -- `http://192.168.8.100:5000`, a LAN address on one developer's
/// machine -- passed CI, passed analyze and passed tests, and produced an APK
/// that rejected every request at the socket layer with no Dart-side error.
///
/// These tests are the missing gate. They cannot prove the API is up, but they
/// fail the moment the default stops being a real public https host, which is
/// the specific regression that reached production.
void main() {
  group('the release API endpoint', () {
    test('defaults to the deployed production host', () {
      expect(
        siteLink,
        'https://api.xetuwiz.de',
        reason:
            'A released APK must reach the deployed API with no build flags. '
            'If this changed, the deploy target changed and every released build '
            'points at a host that no longer exists.',
      );
    });

    test('is https, so a release build can open a socket at all', () {
      // src/main/res/xml/network_security_config.xml sets
      // cleartextTrafficPermitted="false". A plain-http default cannot connect,
      // and `_assertReleaseEndpointIsSecure` only runs in release mode, so this
      // is the check that stands in for it.
      expect(siteLink.startsWith('https://'), isTrue);
    });

    test('carries no /api suffix, because AppUrls appends it', () {
      // Every path is built as `$baseEndPoint/api/...` (see app_urls.dart), so a
      // trailing /api here would produce /api/api/auth/login/ and every endpoint
      // would 404. This is the failure mode of a well-meaning "fix" that adds
      // the prefix here.
      expect(siteLink.endsWith('/api'), isFalse);
      expect(siteLink.endsWith('/'), isFalse, reason: 'would double the slash');
    });

    test('is not a private or loopback address', () {
      // The old default was a 192.168.x.x LAN address. It is syntactically
      // valid, which is exactly why nothing caught it.
      expect(siteLink, isNot(contains('192.168.')));
      expect(siteLink, isNot(contains('10.0.2.2')));
      expect(siteLink, isNot(contains('localhost')));
      expect(siteLink, isNot(contains('127.0.0.1')));
    });
  });

  group('derived endpoints', () {
    test('baseEndPoint returns the configured host', () {
      expect(baseEndPoint, siteLink);
    });

    test('the websocket endpoint is wss, not ws', () {
      // `ws://` is cleartext too, and the release network policy does not exempt
      // sockets -- so a ws endpoint in a release build cannot connect either.
      // The realtime invite and chat screens would fail silently while REST
      // worked, which is a confusing split to debug.
      expect(webSocketEndPoint.startsWith('wss://'), isTrue);
      expect(webSocketEndPoint, 'wss://api.xetuwiz.de');
    });

    test('the websocket endpoint keeps the same host as REST', () {
      // Derived by string surgery on the REST base, so this asserts the surgery
      // touched the scheme and nothing else. If it ever diverged, the realtime
      // screens and the REST screens would be talking to different servers.
      expect(
        webSocketEndPoint,
        siteLink.replaceFirst('https://', 'wss://'),
      );
    });
  });
}