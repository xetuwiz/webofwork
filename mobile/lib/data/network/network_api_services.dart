import 'dart:async';
import 'dart:convert';
import 'dart:io';

import 'package:flutter/foundation.dart';
import 'package:flutter/material.dart';
import '../../helper/extension/string_extension.dart';
import '../../helper/local_keys.g.dart';
import '../../helper/network_connectivity.dart';
import '../../views/sign_in_view/sign_in_view.dart';
import '../../data/network/base_api_services.dart';
import 'package:http/http.dart' as http;

import '../../helper/app_urls.dart';
import '../../helper/constant_helper.dart';
import '../../main.dart';
import '../app_exceptions.dart';

class NetworkApiServices extends BaseApiServices {
  final NetworkConnectivity networkConnectivity = NetworkConnectivity.instance;

  // ── Token Refresh ────────────────────────────────────────────────────────────

  /// True for the auth endpoints, whose 401s are *expected* (bad credentials,
  /// expired OTP) and must never trigger a refresh or a sign-out.
  ///
  /// Without this, a wrong password produced: 401 from /api/auth/login/ ->
  /// _tryRefreshToken() -> no stored refresh -> _redirectToSignIn(), so the
  /// user saw "Session expired" and got navigated instead of "Invalid
  /// credentials".
  static bool _isAuthEndpoint(String url) => url.contains('/api/auth/');

  /// Attempt a single token refresh. Returns true if a new access token was
  /// stored, false otherwise. On failure it clears all tokens and kicks the
  /// user back to the sign-in screen.
  Future<bool> _tryRefreshToken() async {
    final refresh = getRefreshToken;
    if (refresh.isEmpty) {
      _redirectToSignIn();
      return false;
    }
    try {
      final response = await http
          .post(
            Uri.parse(AppUrls.tokenRefreshUrl),
            headers: {'Content-Type': 'application/json', 'Accept': 'application/json'},
            // The backend reads the cookie first and falls back to this field --
            // package:http has no cookie jar, so the body is all we have.
            body: jsonEncode({'refresh': refresh}),
          )
          .timeout(const Duration(seconds: 10));

      if (response.statusCode == 200) {
        final body = jsonDecode(response.body);
        final newAccess = body['access']?.toString() ?? '';
        if (newAccess.isNotEmpty) {
          await setToken(newAccess);
          // Persist a rotated refresh token when the server issues one.
          final newRefresh = body['refresh']?.toString() ?? '';
          if (newRefresh.isNotEmpty) await setRefreshToken(newRefresh);
          debugPrint('[Auth] Token refreshed successfully');
          return true;
        }
      }
    } catch (_) {}
    // Refresh failed — clear everything and go to login.
    _redirectToSignIn();
    return false;
  }

  /// Single-flight guard for [ _redirectToSignIn ].
  ///
  /// A stale-but-present token looks authenticated to the splash screen, so the
  /// dashboard mounts and its four child widgets each fire an authenticated GET
  /// from `initState`. All four 401 at once, and without this guard each one
  /// independently ran `pushAndRemoveUntil`, which races and corrupts the route
  /// history — the post-login `Navigator.pop` then had nothing valid to return
  /// to and the app rendered a black screen.
  static bool _isRedirecting = false;

  /// Cleared as soon as a fresh token exists, so a later expiry can redirect again.
  static void resetAuthRedirectGuard() => _isRedirecting = false;

  void _redirectToSignIn() {
    if (_isRedirecting) return;
    _isRedirecting = true;
    // Deliberately not awaited: the user is being sent to sign-in, and the
    // in-memory cache is already cleared by the time this returns. Blocking the
    // navigation on a keystore round-trip would be a visible delay on an
    // expired session, for no security gain.
    unawaited(clearTokens());
    LocalKeys.sessionExpired.showToast();
    navigatorKey.currentState?.pushAndRemoveUntil(
      MaterialPageRoute(builder: (_) => const SignInView()),
      (route) => false,
    );
  }

  // ── GET ──────────────────────────────────────────────────────────────────────

  @override
  Future<dynamic> getApi(String url, requestName, {headers, timeoutSeconds}) async {
    if (kDebugMode) debugPrint('[GET] $url');

    final hasConnection = await networkConnectivity.currentStatus();
    if (!hasConnection) {
      LocalKeys.noConnectionFound.showToast();
      return null;
    }

    Map<String, String> h = Map.from(headers ?? {});
    h['Accept'] = 'application/json';
    // Default to the stored token, like deleteApi/patchApi do. Without this,
    // any getApi call that omits `headers:` goes out unauthenticated and every
    // [Authorize] endpoint answers 401 -- which the app misreads as an expired
    // session and bounces the user to sign-in. putIfAbsent keeps the caller's
    // own header when one was passed.
    final token = getToken;
    if (token.isNotEmpty) h.putIfAbsent('Authorization', () => 'Bearer $token');

    // Must be `dynamic`, not `Map?`. `returnResponse` decodes whatever the
    // endpoint sent, and several return a bare JSON *array* (e.g.
    // /api/profile/todos -> `[]`, /api/profile/freelancer/recommended-projects
    // -> `[{...}]`). Assigning a List to a Map? threw
    // "type 'List<dynamic>' is not a subtype of type 'Map<dynamic, dynamic>?'",
    // which the catch below turned into a spurious error toast and left the
    // caller with null -- so those lists never rendered.
    dynamic responseJson;
    try {
      final response = await http
          .get(Uri.parse(url), headers: h)
          .timeout(Duration(seconds: timeoutSeconds ?? 10));
      if (kDebugMode) {
        debugPrint('[GET ${response.statusCode}] ${response.body}');
      }

      // Transparent 401 retry with refreshed token.
      if (response.statusCode == 401 && !_isAuthEndpoint(url)) {
        final refreshed = await _tryRefreshToken();
        if (refreshed) {
          h['Authorization'] = 'Bearer $getToken';
          final retryResponse = await http
              .get(Uri.parse(url), headers: h)
              .timeout(Duration(seconds: timeoutSeconds ?? 10));
          responseJson =
              returnResponse(retryResponse, surfaceError: !_isAuthEndpoint(url));
        }
      } else {
        responseJson = returnResponse(response, surfaceError: !_isAuthEndpoint(url));
      }
    } on SocketException {
      debugPrint('[GET] SocketException: $url');
      showError(requestName, LocalKeys.invalidUrl);
    } on RequestTimeOut {
      debugPrint('[GET] Timeout: $url');
      showError(requestName, LocalKeys.invalidUrl);
    } catch (e) {
      debugPrint('[GET] Error: $e');
      showError(requestName, e.toString());
    }
    return responseJson;
  }

  // ── POST ─────────────────────────────────────────────────────────────────────

  @override
  Future<dynamic> postApi(var data, String url, requestName, {headers}) async {
    if (kDebugMode) {
      debugPrint('[POST] $url');
      debugPrint('[POST body] $data');
    }

    final hasConnection = await networkConnectivity.currentStatus();
    if (!hasConnection) {
      LocalKeys.noConnectionFound.showToast();
      return null;
    }

    Map<String, String> h = Map.from(headers ?? {});
    h.putIfAbsent('Accept', () => 'application/json');
    h.putIfAbsent('Content-Type', () => 'application/json');
    // Same default as getApi/deleteApi/patchApi -- protects POST callers that
    // omit `headers:` (e.g. addTodo, completeTodo) from a bogus 401.
    final postToken = getToken;
    if (postToken.isNotEmpty) {
      h.putIfAbsent('Authorization', () => 'Bearer $postToken');
    }

    var bodyData = data;
    if (data is Map && h['Content-Type'] == 'application/json') {
      bodyData = jsonEncode(data);
    }

    // See getApi: must be `dynamic` so a JSON array response is not a cast error.
    dynamic responseJson;
    try {
      final response = await http
          .post(Uri.parse(url), body: bodyData, headers: h)
          .timeout(const Duration(seconds: 10));
      if (kDebugMode) {
        debugPrint('[POST ${response.statusCode}] ${response.body}');
      }

      if (response.statusCode == 401 && !_isAuthEndpoint(url)) {
        final refreshed = await _tryRefreshToken();
        if (refreshed) {
          h['Authorization'] = 'Bearer $getToken';
          final retryResponse = await http
              .post(Uri.parse(url), body: bodyData, headers: h)
              .timeout(const Duration(seconds: 10));
          responseJson =
              returnResponse(retryResponse, surfaceError: !_isAuthEndpoint(url));
        }
      } else {
        responseJson = returnResponse(response, surfaceError: !_isAuthEndpoint(url));
      }
    } on SocketException {
      debugPrint('[POST] SocketException: $url');
      showError(requestName, LocalKeys.invalidUrl);
    } on RequestTimeOut {
      debugPrint('[POST] Timeout: $url');
      showError(requestName, LocalKeys.requestTimeOut);
    } catch (e) {
      showError(requestName, e.toString());
      debugPrint('[POST] Error: $e');
    }
    return responseJson;
  }

  // ── DELETE ───────────────────────────────────────────────────────────────────

  Future<dynamic> deleteApi(String url, requestName, {headers}) async {
    if (kDebugMode) debugPrint('[DELETE] $url');

    final hasConnection = await networkConnectivity.currentStatus();
    if (!hasConnection) {
      LocalKeys.noConnectionFound.showToast();
      return null;
    }

    Map<String, String> h = Map.from(headers ?? {});
    h.putIfAbsent('Accept', () => 'application/json');
    h.putIfAbsent('Authorization', () => 'Bearer $getToken');

    dynamic responseJson;
    try {
      final response = await http
          .delete(Uri.parse(url), headers: h)
          .timeout(const Duration(seconds: 10));

      if (kDebugMode) debugPrint('[DELETE ${response.statusCode}] ${response.body}');

      if (response.statusCode == 401 && !_isAuthEndpoint(url)) {
        final refreshed = await _tryRefreshToken();
        if (refreshed) {
          h['Authorization'] = 'Bearer $getToken';
          final retryResponse = await http
              .delete(Uri.parse(url), headers: h)
              .timeout(const Duration(seconds: 10));
          responseJson =
              returnResponse(retryResponse, surfaceError: !_isAuthEndpoint(url));
        }
      } else {
        responseJson = returnResponse(response, surfaceError: !_isAuthEndpoint(url));
      }
    } on SocketException {
      debugPrint('[DELETE] SocketException');
      showError(requestName, LocalKeys.invalidUrl);
    } on RequestTimeOut {
      debugPrint('[DELETE] Timeout');
      showError(requestName, LocalKeys.requestTimeOut);
    } catch (e) {
      showError(requestName, e.toString());
      debugPrint('[DELETE] Error: $e');
    }
    return responseJson;
  }
  // ── PATCH ────────────────────────────────────────────────────────────────────

  Future<dynamic> patchApi(var data, String url, requestName, {headers}) async {
    if (kDebugMode) {
      debugPrint('[PATCH] $url');
      debugPrint('[PATCH body] $data');
    }

    final hasConnection = await networkConnectivity.currentStatus();
    if (!hasConnection) {
      LocalKeys.noConnectionFound.showToast();
      return null;
    }

    Map<String, String> h = Map.from(headers ?? {});
    h.putIfAbsent('Accept', () => 'application/json');
    h.putIfAbsent('Authorization', () => 'Bearer $getToken');
    if (!h.containsKey('Content-Type')) {
      h['Content-Type'] = 'application/json';
    }

    dynamic responseJson;
    try {
      final response = await http
          .patch(Uri.parse(url), body: data is Map ? jsonEncode(data) : data, headers: h)
          .timeout(const Duration(seconds: 10));
      if (kDebugMode) {
        debugPrint('[PATCH ${response.statusCode}] ${response.body}');
      }

      if (response.statusCode == 401 && !_isAuthEndpoint(url)) {
        final refreshed = await _tryRefreshToken();
        if (refreshed) {
          h['Authorization'] = 'Bearer $getToken';
          final retryResponse = await http
              .patch(Uri.parse(url),
                  body: data is Map ? jsonEncode(data) : data, headers: h)
              .timeout(const Duration(seconds: 10));
          responseJson =
              returnResponse(retryResponse, surfaceError: !_isAuthEndpoint(url));
        }
      } else {
        responseJson = returnResponse(response, surfaceError: !_isAuthEndpoint(url));
      }
    } on SocketException {
      debugPrint('[PATCH] SocketException: $url');
      showError(requestName, LocalKeys.invalidUrl);
    } on RequestTimeOut {
      debugPrint('[PATCH] Timeout: $url');
      showError(requestName, LocalKeys.requestTimeOut);
    } catch (e) {
      showError(requestName, e.toString());
      debugPrint('[PATCH] Error: $e');
    }
    return responseJson;
  }

  // ── PUT ──────────────────────────────────────────────────────────────────────

  /// `PUT` was missing entirely, so the only routes that use it -- the freelancer
  /// payment settings (`/api/profile/freelancer/payment-settings`) -- were
  /// unreachable from this app. Mirrors [patchApi]: same headers, same JSON
  /// encoding, same transparent 401-refresh-and-retry.
  Future<dynamic> putApi(var data, String url, requestName, {headers}) async {
    if (kDebugMode) {
      debugPrint('[PUT] $url');
      debugPrint('[PUT body] $data');
    }

    final hasConnection = await networkConnectivity.currentStatus();
    if (!hasConnection) {
      LocalKeys.noConnectionFound.showToast();
      return null;
    }

    Map<String, String> h = Map.from(headers ?? {});
    h.putIfAbsent('Accept', () => 'application/json');
    h.putIfAbsent('Authorization', () => 'Bearer $getToken');
    if (!h.containsKey('Content-Type')) {
      h['Content-Type'] = 'application/json';
    }

    final body = data is Map ? jsonEncode(data) : data;

    dynamic responseJson;
    try {
      final response = await http
          .put(Uri.parse(url), body: body, headers: h)
          .timeout(const Duration(seconds: 10));
      if (kDebugMode) {
        debugPrint('[PUT ${response.statusCode}] ${response.body}');
      }

      if (response.statusCode == 401 && !_isAuthEndpoint(url)) {
        final refreshed = await _tryRefreshToken();
        if (refreshed) {
          h['Authorization'] = 'Bearer $getToken';
          final retryResponse = await http
              .put(Uri.parse(url), body: body, headers: h)
              .timeout(const Duration(seconds: 10));
          responseJson =
              returnResponse(retryResponse, surfaceError: !_isAuthEndpoint(url));
        }
      } else {
        responseJson = returnResponse(response, surfaceError: !_isAuthEndpoint(url));
      }
    } on SocketException {
      debugPrint('[PUT] SocketException: $url');
      showError(requestName, LocalKeys.invalidUrl);
    } on RequestTimeOut {
      debugPrint('[PUT] Timeout: $url');
      showError(requestName, LocalKeys.requestTimeOut);
    } catch (e) {
      showError(requestName, e.toString());
      debugPrint('[PUT] Error: $e');
    }
    return responseJson;
  }

  // ── Multipart POST ───────────────────────────────────────────────────────────

  Future<dynamic> postWithFileApi(http.MultipartRequest request, requestName,
      {headers}) async {
    if (kDebugMode) {
      debugPrint('[MULTIPART] ${request.url}');
    }
    // Hoisted so the 401 check and the response handler share one string, and so
    // the `url` variable is in scope for returnResponse's auth-endpoint test.
    final url = request.url.toString();

    final hasConnection = await networkConnectivity.currentStatus();
    if (!hasConnection) {
      LocalKeys.noConnectionFound.showToast();
      return null;
    }

    // See getApi: an upload endpoint could also answer with a JSON array.
    dynamic responseJson;
    try {
      final responseStream = await request.send();
      final data = await responseStream.stream.toBytes();
      http.Response response = http.Response.bytes(data, responseStream.statusCode);
      if (kDebugMode) {
        debugPrint('[MULTIPART ${responseStream.statusCode}] ${response.body}');
      }

      if (responseStream.statusCode == 401 && !_isAuthEndpoint(url)) {
        // Re-build the request with the new token and retry.
        final refreshed = await _tryRefreshToken();
        if (refreshed) {
          request.headers['Authorization'] = 'Bearer $getToken';
          final retryStream = await request.send();
          final retryData = await retryStream.stream.toBytes();
          http.Response retryResponse =
              http.Response.bytes(retryData, retryStream.statusCode);
          responseJson =
              returnResponse(retryResponse, surfaceError: !_isAuthEndpoint(url));
        }
      } else {
        responseJson = returnResponse(response, surfaceError: !_isAuthEndpoint(url));
      }
    } on SocketException {
      debugPrint('[MULTIPART] SocketException');
      showError(requestName, LocalKeys.invalidUrl);
    } on RequestTimeOut {
      debugPrint('[MULTIPART] Timeout');
      showError(requestName, LocalKeys.requestTimeOut);
    } catch (e) {
      showError(requestName, e.toString());
      debugPrint('[MULTIPART] Error: $e');
    }
    return responseJson;
  }

  // ── Response parsing ─────────────────────────────────────────────────────────

  /// [surfaceError] controls whether a 4xx body is toasted automatically.
  ///
  /// It is switched off for `/api/auth/*` because those endpoints need bespoke
  /// wording ("Invalid credentials", "Invalid OTP") and the sign-in view already
  /// renders its own message from the body. Every other caller either inspects
  /// the body itself or returns a bare `false`, so without the toast a rejected
  /// write failed in silence.
  dynamic returnResponse(http.Response response, {bool surfaceError = true}) {
    switch (response.statusCode) {
      case 200:
      case 201:
        try {
          return jsonDecode(response.body);
        } catch (_) {
          return {};
        }

      // 204/205 carry no body. These used to fall through to `default`, where
      // `jsonDecode('')` threw and the `catch (_)` swallowed it, so the caller
      // received `null` -- indistinguishable from a network failure. Every
      // NoContent() endpoint in this API therefore reported failure even though
      // the write had already succeeded:
      //   POST   /api/contracts/{id}/accept                 (ContractsController)
      //   PATCH  /api/projects/invitations/{id}/update-status
      //   DELETE /api/deliverables/{id}
      //   DELETE /api/reviews/{id}
      //   PATCH  /api/support/tickets/{id}/cancel
      // An empty map is the honest "2xx with no payload" answer; callers test
      // for `!= null`.
      case 204:
      case 205:
        return <String, dynamic>{};

      case 400:
        try {
          final body = jsonDecode(response.body);
          // Return the body to callers so they can surface field-level errors,
          // and also toast it: a 400 is always a rejected write, and the callers
          // that branch on it (profile edit, add todo) return `false` without
          // saying why.
          if (surfaceError) showValidationErrors(body);
          return body;
        } catch (_) {
          debugPrint('[400] ${response.body}');
          return null;
        }

      case 401:
        // Return the body so callers can surface the reason. The auth endpoints
        // 401 legitimately (bad credentials / expired OTP) and are excluded from
        // the refresh-and-sign-out path, so this is the only chance the caller
        // gets to explain what went wrong.
        try {
          return jsonDecode(response.body);
        } catch (_) {
          debugPrint('[401] ${response.body}');
          return null;
        }

      case 422:
        try {
          final body = jsonDecode(response.body);
          showValidationErrors(body);
        } catch (_) {
          debugPrint('[422] ${response.body}');
        }
        return null;

      default:
        try {
          final body = jsonDecode(response.body);
          showValidationErrors(body);
        } catch (_) {
          // Empty or non-JSON body on a non-2xx. Previously this was a silent
          // debugPrint, so a 404 (wrong URL) or a 405 (wrong HTTP verb) was
          // indistinguishable in the UI from "the server returned an empty
          // list" -- the exact shape of failure behind several dead screens.
          debugPrint('[${response.statusCode}] ${response.body}');
          if (surfaceError) {
            LocalKeys.requestFailed.showToast();
          }
        }
        return null;
    }
  }

  showError(requestName, error) {
    if (requestName != null) {
      '$requestName: $error'.showToast();
    }
  }

  showValidationErrors(dynamic json) {
    // Error bodies are not always objects -- an endpoint that normally returns a
    // list can still fail with a JSON array. Two shapes show up in this API:
    //   * a JSON *array* of FluentValidation ValidationFailure objects
    //     (e.g. `BadRequest(validationResult.Errors)` in ProposalsController /
    //     InvitationsController), and
    //   * a bare object.
    // The array form used to be dropped on the floor entirely, so a rejected
    // proposal or invitation failed with no message at all.
    if (json is List) {
      for (final item in json) {
        if (item is Map && item['error_message'] != null) {
          item['error_message'].toString().showToast();
          return;
        }
      }
      debugPrint('[validation-error] $json');
      return;
    }
    if (json is! Map) {
      debugPrint('[validation-error] $json');
      return;
    }
    if (!json.containsKey('errors') || json['errors'] == {}) {
      // `error` is the key every business-rule rejection in this API uses --
      // e.g. `BadRequest(new { error = ex.Message })` in ContractsController,
      // DeliverablesController, InvitationsController, ChatController,
      // TodosController. Reading only `message`/`detail` meant a rejected
      // contract, an oversized upload, a blocked chat message or an
      // over-limit todo all failed in silence.
      final msg = json['error']?.toString() ??
          json['message']?.toString() ??
          json['detail']?.toString();
      if (msg != null && msg.isNotEmpty) msg.showToast();
      return;
    }

    final errors = json['errors'];
    if (errors is Map && errors.isNotEmpty) {
      final keys = errors.keys.toList();
      keys.remove('status');
      if (keys.isEmpty) return;
      final firstError = errors[keys.first];
      String? message;
      if (firstError is String) {
        message = firstError;
      } else if (firstError is List && firstError.isNotEmpty) {
        message = firstError.first.toString();
      }
      if (message != null) message.showToast();
    }
  }
}
