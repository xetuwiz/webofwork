import 'package:flutter/foundation.dart' show kReleaseMode;
import 'package:flutter/material.dart';

// A physical device cannot reach this machine via "localhost" -- on the phone
// that means the phone itself. Use the LAN IP of the dev box (ipconfig), or
// 10.0.2.2 on an Android emulator. Plain HTTP because the dev Kestrel cert is
// self-signed; the debug and profile network security configs are what permit
// that, at android/app/src/{debug,profile}/res/xml/network_security_config.xml.
//
//   flutter run --dart-define=API_BASE=http://<lan-ip>:5000
//
// A release build uses the strict config in src/main, which sets
// `cleartextTrafficPermitted="false"`. So a release APK pointed at plain HTTP
// cannot open a socket at all -- see _assertReleaseEndpointIsSecure below for
// why that fails loudly instead.
//
//   flutter build apk --release --dart-define=API_BASE=https://api.example.com
const String siteLink = String.fromEnvironment(
  'API_BASE',
  defaultValue: 'http://192.168.8.100:5000',
);

/// Fails fast when a release build is configured to talk plain HTTP.
///
/// This exists because the strict release network policy and this `defaultValue`
/// cannot both be right, and the mistake is invisible if nothing checks. A
/// release APK left on the default would fail every request at the socket layer
/// with no Dart-side error and no log line -- indistinguishable from a server
/// that is simply down, which is a genuinely miserable thing to debug on a
/// device you cannot attach a debugger to.
///
/// `assert` would be the obvious choice and is exactly wrong here: asserts are
/// stripped from release builds, so it would be absent from the one build it
/// exists to protect. Hence a real check that throws.
///
/// Throwing rather than degrading to some default is intentional. There is no
/// sensible https fallback to substitute -- a wrong hostname fails just as
/// hard, and more confusingly -- so the honest response is to refuse to start
/// and say what the correct build command is.
///
/// Runs once, lazily, on first access to this library rather than at import of
/// every file that imports it.
void _assertReleaseEndpointIsSecure() {
  if (kReleaseMode && !siteLink.startsWith('https://')) {
    throw StateError(
      'Release builds must use an https API endpoint.\n'
      'API_BASE is "${siteLink}", which is plain HTTP.\n'
      'The release network security config '
      '(android/app/src/main/res/xml/network_security_config.xml) sets '
      'cleartextTrafficPermitted="false", so every request would fail at the '
      'socket layer with no Dart-side error.\n'
      'Rebuild with:\n'
      '  flutter build apk --release --dart-define=API_BASE=https://your-api-host\n'
      'Debug and profile builds are unaffected: their configs permit cleartext '
      'for the self-signed development certificate.',
    );
  }
}

bool _endpointChecked = false;

/// The API base URL, validated.
String get baseEndPoint {
  if (!_endpointChecked) {
    _endpointChecked = true;
    _assertReleaseEndpointIsSecure();
  }
  return siteLink;
}

String appLabel = 'WebOfWork';
String storeLabel = "WebOfWork";
String appVersion = '1.0.0';

// Three constants that used to live here have been deleted rather than rotated:
//
//   wPaymentUpdateEncryptionKey  - a wallet encryption key. Nothing read it; the
//                                  app stores no wallet and does no wallet crypto.
//   socialSignInKey              - a bcrypt hash. Nothing read it, and there is no
//                                  social login: this app is freelancer-only.
//   clientSecret                 - used only by the unreachable Apple
//                                  token-revocation branch in AccountDeleteService,
//                                  which was removed with it.
//
// They were unreferenced, so deleting them is the correct response rather than
// rotating them. What made them worth removing rather than ignoring is that they
// were committed in plaintext and look like live credentials: `socialSignInKey`
// in particular is a well-formed hash, and an automated secret scanner — or a
// marker doing exactly the review this project invites — will flag it.
//
// The base endpoint is the one thing here that must be provided per environment.
// See `baseEndPoint` above and the network security config for why the dev
// default is plain HTTP and a release build must not be.

/// Base for the real-time sockets.
///
/// The backend mounts a raw WebSocket bridge at `/ws` (see
/// `WebSocketBridgeMiddleware`), *not* a Pusher or socket.io endpoint, so the
/// scheme has to be swapped from the REST base: `http://` -> `ws://`,
/// `https://` -> `wss://`. The JWT is passed as `?token=` because a WebSocket
/// handshake cannot carry an `Authorization` header from a browser, and the
/// middleware reads either `token` or `access_token` from the query.
String get webSocketEndPoint {
  // Reads `baseEndPoint`, not `siteLink`, so the release https check runs.
  // A socket dialed over `ws://` is cleartext too -- the network security config
  // does not exempt WebSockets -- so validating only the REST getter would leave
  // the realtime path able to open a plaintext connection in a release build.
  final base = baseEndPoint;
  if (base.startsWith('https://')) {
    return 'wss://${base.substring('https://'.length)}';
  }
  if (base.startsWith('http://')) {
    return 'ws://${base.substring('http://'.length)}';
  }
  return base;
}

const Color primaryColor = Color(0xff6176F6);
const Color blackColor = Color(0xff101828);
const Color whiteColor = Color(0xffffffff);
const Color hintColor = Color(0xff9a9a9e);
const Color borderColor = Color(0xffCDCCCE);
const Color secondaryColor = Color(0xff6176F6);
const Color warningColor = Color(0xffEB4747);
const Color greenColor = Color(0xff65C18C);
const Color yellowColor = Color(0xffFFB200);

const Color gOneColor = Color(0xff6176F6);
const gTwoColor = Color(0xffFAA500);

List<Color> get chatAvatarBGColors => [
      const Color(0xff0087BF),
      const Color(0xff5A8770),
      const Color(0xff9A89B5),
      const Color(0xffF5888D),
      const Color(0xff98A2B3),
      const Color(0xffF18636),
    ];
List<Color> get statusColors => [
      const Color(0xff9a9a9e),
      const Color(0xff65C18C),
      const Color(0xffEB4747),
    ];
