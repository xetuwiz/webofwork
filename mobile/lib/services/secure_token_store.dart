import 'package:flutter/foundation.dart';
import 'package:flutter_secure_storage/flutter_secure_storage.dart';

/// Where the session tokens live.
///
/// ## Why this is an interface
///
/// `flutter_secure_storage` talks to platform channels, so it cannot be used in
/// a widget test without a mock -- and mocking a platform channel in every test
/// that transitively reads a token would mean most tests know about storage.
/// Through an interface, the app injects the real implementation once at
/// startup and tests inject a plain map.
abstract class TokenStore {
  /// Synchronously read [key]. Empty string when absent.
  String read(String key);

  /// Persist [value] under [key]; `null` clears it.
  Future<void> write(String key, String? value);

  /// Remove [key].
  Future<void> delete(String key);
}

/// The real store: encrypted platform storage, with an in-memory read cache.
///
/// ## Why there is a cache, and why that is not a security compromise
///
/// `FlutterSecureStorage` is asynchronous. Every token read in this app is not.
/// `getToken` is called from roughly twenty places that build an
/// `Authorization` header synchronously, and making those `Future<String>` would
/// push an `await` through the whole networking layer and most of the view
/// models -- a change with a real chance of introducing a bug in the one path
/// that authenticates every request, in exchange for no security gain.
///
/// So the platform is the source of truth and memory is a read cache in front of
/// it. The cache is not the weak link it looks like: the token is already in
/// process memory inside every `Authorization` header this app builds, and any
/// code that can read those can read this. What the cache does *not* do is leave
/// a copy lying on disk where a backup or a file browser can find it -- which is
/// the actual exposure that was fixed.
///
/// Writes go straight through to the encrypted store, so a token is durable
/// before the future completes; a crash cannot lose one.
class SecureTokenStore implements TokenStore {
  SecureTokenStore({FlutterSecureStorage? storage})
    : _storage = storage ?? const FlutterSecureStorage();

  final FlutterSecureStorage _storage;

  /// The read cache. Populated by [hydrate] before any request is made.
  final Map<String, String> _cache = <String, String>{};

  /// Android options are set explicitly rather than left to the defaults.
  ///
  /// The default on Android is `encryptedSharedPreferences`, which is already
  /// correct, but naming it means a future default change cannot silently
  /// downgrade this to plain preferences -- which is exactly the regression
  /// this class exists to prevent.
  static const AndroidOptions _androidOptions = AndroidOptions(
    encryptedSharedPreferences: true,
  );

  /// Load persisted values into the cache.
  ///
  /// Called once during startup, before the splash screen decides whether the
  /// user is signed in. Deliberately tolerant: a store that cannot be read must
  /// not prevent the app from launching, because an unreadable token store means
  /// "signed out", not "crash".
  Future<void> hydrate() async {
    try {
      for (final key in _tokenKeys) {
        final value = await _storage.read(key: key, aOptions: _androidOptions);
        if (value != null && value.isNotEmpty) {
          _cache[key] = value;
        }
      }
    } catch (error, stack) {
      // Logged rather than swallowed silently: a store that fails on every
      // launch is a bug, and "signed out with no explanation" is the symptom
      // that gets reported as "the app logs me out randomly".
      debugPrint('[TokenStore] could not read secure storage: $error');
      debugPrintStack(stackTrace: stack, maxFrames: 6);
    }
  }

  /// The keys this store owns. Fixed rather than discovered, so a stray key
  /// written by an older build cannot be read into the cache.
  static const List<String> _tokenKeys = <String>['token', 'refresh_token'];

  /// Adopt [legacy] for [key] if the store has nothing for it.
  ///
  /// Used once, by [migrateLegacy], to move tokens written by a previous
  /// version out of `SharedPreferences`. Without it, upgrading the app would
  /// sign every existing user out -- a self-inflicted outage on release day, and
  /// the kind of bug that is only found by users.
  Future<bool> adopt(String key, String? legacy) async {
    if (legacy == null || legacy.isEmpty) return false;
    if (_cache.containsKey(key)) return false;

    _cache[key] = legacy;
    try {
      await _storage.write(key: key, value: legacy, aOptions: _androidOptions);
      return true;
    } catch (error) {
      debugPrint('[TokenStore] could not migrate $key: $error');
      // Dropped from the cache on failure: keeping it in memory would make the
      // session appear to work and then vanish on the next launch, which is
      // harder to report than a clean sign-out.
      _cache.remove(key);
      return false;
    }
  }

  @override
  String read(String key) => _cache[key] ?? '';

  @override
  Future<void> write(String key, String? value) async {
    // Cache first, so a caller that writes and immediately reads sees its own
    // write even before the platform call completes.
    if (value == null || value.isEmpty) {
      _cache.remove(key);
    } else {
      _cache[key] = value;
    }

    try {
      await _storage.write(key: key, value: value, aOptions: _androidOptions);
    } catch (error) {
      // The in-memory value is deliberately left in place. The user has a valid
      // session for this launch either way; failing the request that just
      // succeeded because the disk write failed would be worse. The next launch
      // signs them out, which is the correct and recoverable outcome.
      debugPrint('[TokenStore] could not persist $key: $error');
    }
  }

  @override
  Future<void> delete(String key) async {
    _cache.remove(key);
    try {
      await _storage.delete(key: key, aOptions: _androidOptions);
    } catch (error) {
      debugPrint('[TokenStore] could not delete $key: $error');
    }
  }

  /// Remove every token this app owns.
  Future<void> clearAll() async {
    for (final key in _tokenKeys) {
      _cache.remove(key);
    }
    try {
      await _storage.deleteAll(aOptions: _androidOptions);
    } catch (error) {
      debugPrint('[TokenStore] could not clear secure storage: $error');
    }
  }
}

/// An in-memory store.
///
/// For tests, and for the rare platform where secure storage is unavailable --
/// a desktop debug run, for instance -- where an in-memory session beats a
/// plaintext one.
class InMemoryTokenStore implements TokenStore {
  final Map<String, String> _values = <String, String>{};

  @override
  String read(String key) => _values[key] ?? '';

  @override
  Future<void> write(String key, String? value) async {
    if (value == null || value.isEmpty) {
      _values.remove(key);
    } else {
      _values[key] = value;
    }
  }

  @override
  Future<void> delete(String key) async => _values.remove(key);
}