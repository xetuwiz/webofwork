import 'package:flutter/material.dart';
import 'package:provider/provider.dart';
import 'package:shared_preferences/shared_preferences.dart';

import '/services/app_string_service.dart';
import '../services/dynamics/dynamics_service.dart';
import '../services/secure_token_store.dart';
import 'web_socket_service.dart';

/// The app's navigator key, used to push routes and raise banners from code that
/// has no `BuildContext` of its own (token refresh, socket pushes).
///
/// It lives here rather than in `main.dart` because that file pulls in
/// `firebase_core`, `firebase_messaging` and `flutter_local_notifications`; a
/// view that needed the key would have to import all three to reach it.
/// `main.dart` re-exports it, so the existing call sites are unaffected.
final GlobalKey<NavigatorState> navigatorKey =
    GlobalKey<NavigatorState>(debugLabel: "nav_key");

late DynamicsService dProvider;
late AppStringService asProvider;
SharedPreferences? sPref;

var _conversationId;

get conversationId => _conversationId;
setConversationId(id) {
  _conversationId = id;
}

var _orderId;

get orderId => _orderId;
setOrderId(id) {
  _orderId = id;
}

// �� WebSocket transport ���������������������������������������������������

/// The app-wide [WebSocketService], for code that runs without a `BuildContext`.
///
/// Nullable on purpose: `coreInit` is what fills it, and that is called from
/// `build` in several screens, so anything reaching for this before the first
/// frame would otherwise hit a `LateInitializationError`. Every caller uses
/// `?.`, making an early call a no-op rather than a crash.
///
/// It is the *same* instance the provider hands out: `Providers.providers`
/// registers `WebSocketService` with `create:`, which is lazy, and `coreInit`
/// reads it -- so whoever reads it first owns the single socket both paths see.
WebSocketService? _webSocket;

WebSocketService? get webSocket => _webSocket;

set webSocket(WebSocketService? value) {
  _webSocket = value;
}

// ── Tokens ────────────────────────────────────────────────────────────────────
//
// These live in the platform keystore, not in `SharedPreferences`. See
// `SecureTokenStore` for why the reads stay synchronous while the store is
// asynchronous.
//
// `sPref` is still consulted once, on first run, to move tokens written by a
// build from before this change. That path is bounded by a single migration at
// startup and then deletes the plaintext copies -- see [migrateLegacyTokens].

TokenStore? _tokenStore;

/// The store the token helpers read and write.
///
/// Replaceable so tests can supply an [InMemoryTokenStore] rather than standing
/// up a platform channel. Production code never assigns this.
TokenStore? get tokenStore => _tokenStore;

setTokenStore(TokenStore? store) {
  _tokenStore = store;
}

/// Adopt a plaintext token from an older build, then remove it.
///
/// Returns the number of tokens migrated, so startup can tell the difference
/// between "already migrated" and "nothing to do" without logging either.
Future<int> migrateLegacyTokens() async {
  final store = _tokenStore;
  if (store is! SecureTokenStore) return 0;

  var migrated = 0;
  for (final key in const ['token', 'refresh_token']) {
    final legacy = sPref?.getString(key);
    if (legacy == null || legacy.isEmpty) continue;

    if (await store.adopt(key, legacy)) migrated++;
    // Deleted whether or not the write succeeded: leaving it behind would keep
    // a plaintext copy on disk indefinitely, which is the thing being removed.
    await sPref?.remove(key);
  }
  return migrated;
}

// ── Access token ──────────────────────────────────────────────────────────────

String get getToken => _tokenStore?.read('token') ?? '';

Future<void> setToken(String? token) async {
  await _tokenStore?.write('token', token);
}

// ── Refresh token ─────────────────────────────────────────────────────────────

String get getRefreshToken => _tokenStore?.read('refresh_token') ?? '';

Future<void> setRefreshToken(String? token) async {
  await _tokenStore?.write('refresh_token', token);
}

/// Clear both tokens (logout / session expiry).
Future<void> clearTokens() async {
  final store = _tokenStore;
  if (store is! SecureTokenStore) {
    await _tokenStore?.delete('token');
    await _tokenStore?.delete('refresh_token');
    return;
  }
  await store.clearAll();
}

// ── Common headers ────────────────────────────────────────────────────────────

get commonAuthHeader => {'Authorization': 'Bearer $getToken'};
get acceptJsonHeader => {'Accept': 'application/json'};
get acceptJsonAuthHeader =>
    {'Accept': 'application/json', 'Authorization': 'Bearer $getToken'};

// ── App init ──────────────────────────────────────────────────────────────────

/// Load persisted tokens into the store's cache.
  ///
  /// Must complete before the first authenticated request, because every token
  /// read is synchronous and therefore reads the cache rather than the platform.
  /// `splash_view_model` awaits this before deciding whether the user is signed
  /// in, and that ordering is the point: asking later would render the
  /// signed-out state for a moment and then flip, which looks like the app
  /// logging the user out on every launch.
  ///
  /// Hydrate before migrating. Migrating first would populate the cache from
  /// plaintext for an already-migrated user whose legacy values are gone, and
  /// hydrate would then overwrite them -- harmless today, but it makes the two
  /// steps' precedence depend on nothing observable.
  Future<void> initiateToken() async {
    _tokenStore ??= SecureTokenStore();
    await (_tokenStore as SecureTokenStore).hydrate();

    final migrated = await migrateLegacyTokens();
    if (migrated > 0) {
      debugPrint('[Auth] moved $migrated token(s) out of SharedPreferences');
    }
  }

coreInit(BuildContext context) async {
  dProvider = Provider.of<DynamicsService>(context, listen: false);
  asProvider = Provider.of<AppStringService>(context, listen: false);
  sPref ??= await SharedPreferences.getInstance();
  // Read once and cache in the global: `coreInit` runs from `build` in several
  // screens, and re-reading the provider there would make each of those screens
  // a build dependency of the socket.
  webSocket ??= Provider.of<WebSocketService>(context, listen: false);
}
