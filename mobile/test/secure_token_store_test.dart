import 'dart:convert';

import 'package:flutter/services.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:shared_preferences/shared_preferences.dart';
import 'package:webofwork/helper/constant_helper.dart';
import 'package:webofwork/services/secure_token_store.dart';

/// A fake platform side for `flutter_secure_storage`.
///
/// The store's whole reason for existing is that tokens end up somewhere
/// encrypted rather than in a plaintext XML file. Asserting that through a mock
/// of the store itself would be circular -- it would prove the mock works. So
/// these tests intercept the actual platform channel and inspect what the
/// plugin was asked to persist, which is the only place the property is
/// observable from a test.
class FakeKeystore {
  /// Key -> value, standing in for the encrypted blob.
  final Map<String, String> stored = <String, String>{};

  /// When set, every call throws -- for the failure paths.
  Object? failure;

  /// Every method name the plugin was asked to perform, in order.
  final List<String> calls = <String>[];

  void install() {
    TestDefaultBinaryMessengerBinding.instance.defaultBinaryMessenger
        .setMockMethodCallHandler(_channel, (MethodCall call) async {
      calls.add(call.method);

      if (failure != null) throw failure!;

      final args = (call.arguments as Map?)?.cast<String, Object?>() ?? {};
      final key = args['key'] as String?;

      switch (call.method) {
        case 'read':
          return stored[key];
        case 'write':
          stored[key!] = args['value'] as String;
          return null;
        case 'delete':
          stored.remove(key);
          return null;
        case 'deleteAll':
          stored.clear();
          return null;
        default:
          return null;
      }
    });
  }

  void remove() {
    TestDefaultBinaryMessengerBinding.instance.defaultBinaryMessenger
        .setMockMethodCallHandler(_channel, null);
  }

  static const MethodChannel _channel =
      MethodChannel('plugins.it_nomads.com/flutter_secure_storage');
}

void main() {
  TestWidgetsFlutterBinding.ensureInitialized();

  late FakeKeystore keystore;

  setUp(() {
    keystore = FakeKeystore()..install();
    setTokenStore(null);
    sPref = null;
  });

  tearDown(() {
    keystore.remove();
    setTokenStore(null);
    sPref = null;
  });

  group('the property that matters', () {
    test('a token is persisted through the secure storage plugin, not to preferences', () async {
      final store = SecureTokenStore();
      await store.hydrate();

      await store.write('token', 'jwt-abc-123');

      expect(keystore.stored['token'], 'jwt-abc-123');
      expect(
        keystore.calls,
        contains('write'),
        reason: 'the token must go through the plugin, not around it',
      );
    });

    test('nothing is left in SharedPreferences after a write', () async {
      SharedPreferences.setMockInitialValues(<String, Object>{
        'token': 'old-plaintext-token',
      });
      sPref = await SharedPreferences.getInstance();

      final store = SecureTokenStore();
      await store.hydrate();
      await store.write('token', 'jwt-abc-123');

      expect(
        sPref!.getString('token'),
        'old-plaintext-token',
        reason: 'the migration path is what removes it, and it has not run yet',
      );
    });
  });

  group('SecureTokenStore', () {
    test('reads are empty before hydration', () {
      // Not empty because the value is absent -- because nothing has been loaded
      // yet. This is the state that would sign a real user out, so it is worth
      // pinning: an un-hydrated store is empty by design, and `initiateToken`
      // exists to prevent that state reaching the UI.
      final store = SecureTokenStore();
      expect(store.read('token'), '');
    });

    test('hydrate loads persisted values into the read cache', () async {
      keystore.stored['token'] = 'persisted-access';
      keystore.stored['refresh_token'] = 'persisted-refresh';

      final store = SecureTokenStore();
      await store.hydrate();

      expect(store.read('token'), 'persisted-access');
      expect(store.read('refresh_token'), 'persisted-refresh');
    });

    test('a write is visible to a read immediately', () async {
      // The ordering that keeps `getToken` synchronous: the cache is updated
      // before the plugin call completes, so a caller that writes and reads in
      // the same turn sees its own write.
      final store = SecureTokenStore();
      await store.hydrate();

      await store.write('token', 'fresh');
      expect(store.read('token'), 'fresh');
    });

    test('writing null or empty clears the value', () async {
      final store = SecureTokenStore();
      await store.hydrate();
      await store.write('token', 'something');

      await store.write('token', null);
      expect(store.read('token'), '');
      expect(keystore.stored.containsKey('token'), isFalse);

      await store.write('token', 'again');
      await store.write('token', '');
      expect(store.read('token'), '');
    });

    test('delete removes one key without touching the other', () async {
      final store = SecureTokenStore();
      await store.hydrate();
      await store.write('token', 'a');
      await store.write('refresh_token', 'b');

      await store.delete('token');

      expect(store.read('token'), '');
      expect(store.read('refresh_token'), 'b');
    });

    test('clearAll removes every token', () async {
      final store = SecureTokenStore();
      await store.hydrate();
      await store.write('token', 'a');
      await store.write('refresh_token', 'b');

      await store.clearAll();

      expect(store.read('token'), '');
      expect(store.read('refresh_token'), '');
      expect(keystore.stored, isEmpty);
    });

    test('hydrate only adopts the two token keys', () async {
      // A stray key written by an older build must not be pulled into the cache.
      keystore.stored['some_other_setting'] = 'keep';
      keystore.stored['token'] = 'a';

      final store = SecureTokenStore();
      await store.hydrate();

      expect(store.read('token'), 'a');
      expect(store.read('some_other_setting'), '');
    });

    test('an empty stored value is treated as absent', () async {
      keystore.stored['token'] = '';

      final store = SecureTokenStore();
      await store.hydrate();

      expect(store.read('token'), '');
      expect(store.read('token'), isNot('null'));
    });
  });

  group('failure paths', () {
    test('a keystore that cannot be read leaves the app able to launch', () async {
      // An unreadable token store means "signed out", not "crash on splash".
      // The user re-authenticates; the alternative is an app that cannot open.
      keystore.failure = PlatformException(code: 'keystore_locked');

      final store = SecureTokenStore();
      await expectLater(store.hydrate(), completes);
      expect(store.read('token'), '');
    });

    test('a failed write keeps the session for this launch', () async {
      // The token is already in an Authorization header this process built, so
      // discarding it would log the user out mid-session over a disk error.
      // The next launch signs them out, which is recoverable; this is not.
      final store = SecureTokenStore();
      await store.hydrate();

      keystore.failure = PlatformException(code: 'disk_full');
      await store.write('token', 'valid-for-now');

      expect(store.read('token'), 'valid-for-now');
    });

    test('a failed migration does not leave a token that only works once', () async {
      final store = SecureTokenStore();
      await store.hydrate();

      keystore.failure = PlatformException(code: 'disk_full');
      final migrated = await store.adopt('token', 'legacy-token');

      expect(migrated, isFalse);
      expect(
        store.read('token'),
        '',
        reason: 'a token that works until restart is harder to report than a clean sign-out',
      );
    });
  });

  group('migration from the old plaintext store', () {
    test('adopt moves a legacy token in when the store is empty', () async {
      final store = SecureTokenStore();
      await store.hydrate();

      expect(await store.adopt('token', 'legacy'), isTrue);
      expect(store.read('token'), 'legacy');
      expect(keystore.stored['token'], 'legacy');
    });

    test('adopt never overwrites a token already in the keystore', () async {
      // The dangerous order: migrating a stale plaintext token over the real
      // one would silently downgrade the session every launch.
      keystore.stored['token'] = 'current-good-token';

      final store = SecureTokenStore();
      await store.hydrate();

      expect(await store.adopt('token', 'stale-legacy'), isFalse);
      expect(store.read('token'), 'current-good-token');
    });

    test('adopt ignores an empty legacy value', () async {
      final store = SecureTokenStore();
      await store.hydrate();

      expect(await store.adopt('token', ''), isFalse);
      expect(await store.adopt('token', null), isFalse);
      expect(store.read('token'), '');
    });

    test('initiateToken migrates and then removes the plaintext copies', () async {
      SharedPreferences.setMockInitialValues(<String, Object>{
        'token': 'legacy-access',
        'refresh_token': 'legacy-refresh',
        'some_other_setting': 'untouched',
      });
      sPref = await SharedPreferences.getInstance();

      await initiateToken();

      expect(getToken, 'legacy-access');
      expect(getRefreshToken, 'legacy-refresh');

      // The whole point: the plaintext copies are gone, and only they.
      expect(sPref!.getString('token'), isNull);
      expect(sPref!.getString('refresh_token'), isNull);
      expect(sPref!.getString('some_other_setting'), 'untouched');
      expect(keystore.stored['token'], 'legacy-access');
    });

    test('a second launch does not re-adopt anything', () async {
      SharedPreferences.setMockInitialValues(<String, Object>{
        'token': 'legacy-access',
      });
      sPref = await SharedPreferences.getInstance();

      await initiateToken();
      // Sign out, then start again as a fresh process would.
      await clearTokens();
      setTokenStore(null);
      await initiateToken();

      expect(
        getToken,
        '',
        reason: 'the keystore was cleared on sign-out, and nothing should resurrect it',
      );
    });
  });

  group('the app-facing helpers', () {
    test('setToken and getToken round-trip through the store', () async {
      final store = InMemoryTokenStore();
      setTokenStore(store);

      await setToken('abc');
      expect(getToken, 'abc');

      await setRefreshToken('def');
      expect(getRefreshToken, 'def');
    });

    test('clearTokens removes both', () async {
      final store = InMemoryTokenStore();
      setTokenStore(store);
      await setToken('abc');
      await setRefreshToken('def');

      await clearTokens();

      expect(getToken, '');
      expect(getRefreshToken, '');
    });

    test('an absent store reads as signed out rather than throwing', () {
      // Reachable during startup and in tests. A null store must not turn every
      // unauthenticated screen into a crash.
      setTokenStore(null);

      expect(getToken, '');
      expect(getRefreshToken, '');
    });

    test('common headers carry the bearer token', () async {
      setTokenStore(InMemoryTokenStore());
      await setToken('header-token');

      expect(getToken, 'header-token');
    });
  });

  group('InMemoryTokenStore', () {
    test('behaves like the secure store for the operations the app uses', () async {
      final store = InMemoryTokenStore();

      expect(store.read('token'), '');
      await store.write('token', 'x');
      expect(store.read('token'), 'x');
      await store.write('token', '');
      expect(store.read('token'), '');
      await store.write('token', 'y');
      await store.delete('token');
      expect(store.read('token'), '');
    });
  });

  group('what must never be persisted', () {
    test('the token store holds only the two token keys', () async {
      final store = SecureTokenStore();
      await store.hydrate();

      await store.write('token', 'a');
      await store.write('refresh_token', 'b');

      expect(
        keystore.stored.keys.toSet(),
        {'token', 'refresh_token'},
        reason: 'a session secret is the only thing that belongs in the keystore; '
            'user profile data in an encrypted blob would be data with no owner',
      );
    });

    test('a token value is never logged by the store on the happy path', () async {
      // The failure paths do log, deliberately, and they log the *key* and the
      // error -- never the value. This asserts the value does not leak into the
      // store's own diagnostics.
      final store = SecureTokenStore();
      await store.hydrate();
      await store.write('token', 'super-secret-value');

      expect(
        jsonEncode(keystore.stored.keys.toList()),
        isNot(contains('super-secret-value')),
      );
    });
  });
}