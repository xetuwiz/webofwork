import 'package:flutter/material.dart';

import 'package:webofwork/data/network/network_api_services.dart';
import 'package:webofwork/helper/app_urls.dart';
import 'package:webofwork/helper/constant_helper.dart';
import 'package:webofwork/helper/extension/string_extension.dart';
import 'package:webofwork/helper/json_coercion.dart';
import 'package:webofwork/helper/local_keys.g.dart';

/// `Models/Enums/ProposalEnums.cs`:
///   Pending = 0, Accepted = 1, Declined = 2, Cancelled = 3
///
/// Serialized through `JsonStringEnumConverter(SnakeCaseLower)`
/// (`Program.cs:79`), so on the wire these are **lower snake_case strings**.
abstract final class InvitationStatus {
  static const String pending = 'pending';
  static const String accepted = 'accepted';
  static const String declined = 'declined';
  static const String cancelled = 'cancelled';

  /// Everything the server will ever send, in enum order.
  static const List<String> all = [pending, accepted, declined, cancelled];

  /// Folds any historical spelling onto the wire value.
  ///
  /// The previous code compared against the template's int-as-string shape
  /// (`'0'` = pending, `'1'` = accepted, `'4'` = hired). This API has no ints
  /// and **no `hired` state at all** -- `InvitationStatus` stops at
  /// `Cancelled` -- so every one of those comparisons was permanently false:
  /// the "Pending" and "Accepted" tabs rendered empty, the "Hired" tab was
  /// unreachable by construction, and `isPending` was always false, which meant
  /// the Accept/Decline buttons were never built. The freelancer had no way to
  /// answer an invitation from the app at all.
  static String normalize(Object? raw) {
    final value = raw?.toString().trim().toLowerCase() ?? '';
    if (all.contains(value)) return value;
    // Numeric spellings, in case an endpoint ever falls back to the int form.
    switch (value) {
      case '0':
        return pending;
      case '1':
        return accepted;
      case '2':
        return declined;
      case '3':
        return cancelled;
      default:
        return value.isEmpty ? pending : value;
    }
  }

  /// Capitalised label for the status pill.
  static String label(String status) {
    switch (status) {
      case pending:
        return LocalKeys.pending;
      case accepted:
        return LocalKeys.accepted;
      case declined:
        return LocalKeys.declined;
      case cancelled:
        return LocalKeys.cancelled;
      default:
        return status;
    }
  }
}

class Invitation {
  final int id;
  final String clientName;
  final String projectTitle;
  final String? message;

  /// Always one of [InvitationStatus.all] -- see [InvitationStatus.normalize].
  final String status;
  final DateTime? createdAt;

  /// `package_amount` is a `decimal?` on the server, so it arrives as a JSON
  /// **string**. `contract_info` is a free-form `object?` and is only populated
  /// once an invitation has become a contract.
  final String? packageType;
  final String? packageAmount;
  final Map<String, dynamic>? contractInfo;

  /// Why this freelancer was matched, when the invitation came from an
  /// AI-assisted shortlist. Null for a hand-picked invitation.
  ///
  /// The distinction matters to the person reading it: one of these is a claim
  /// the platform made about them, the other is a client's own choice. Showing
  /// the same card for both would misrepresent a judgement as a fact.
  final MatchContext? matchContext;

  Invitation({
    required this.id,
    required this.clientName,
    required this.projectTitle,
    this.message,
    required this.status,
    this.createdAt,
    this.packageType,
    this.packageAmount,
    this.contractInfo,
    this.matchContext,
  });

  factory Invitation.fromJson(Map<String, dynamic> json) {
    return Invitation(
      id: asInt(json['id']),
      // The server projects `client_name`; the `clientName` fallback is dead
      // weight but harmless, and `client_id` is the useful identity.
      clientName:
          asOptString(json['client_name']) ?? asOptString(json['clientName']) ?? '',
      projectTitle:
          asOptString(json['project_title']) ?? asOptString(json['projectTitle']) ?? '',
      message: asOptString(json['message']),
      status: InvitationStatus.normalize(json['status']),
      createdAt: asDateOrNull(json['created_at']),
      packageType: asOptString(json['package_type']),
      packageAmount: asOptString(json['package_amount']),
      contractInfo: asObjectMap(json['contract_info']),
      matchContext: MatchContext.fromJsonOrNull(asObjectMap(json['match_context'])),
    );
  }

  bool get isPending => status == InvitationStatus.pending;

  /// The only states that still accept a decision. `declined`/`cancelled` are
  /// terminal, and re-PATCHing them is rejected by the service.
  bool get isActionable => isPending;

  Invitation copyWith({String? status}) => Invitation(
        id: id,
        clientName: clientName,
        projectTitle: projectTitle,
        message: message,
        status: status ?? this.status,
        createdAt: createdAt,
        packageType: packageType,
        packageAmount: packageAmount,
        contractInfo: contractInfo,
        matchContext: matchContext,
      );
}

/// The AI's reasoning for one recommendation, as shown to the freelancer it is
/// about.
class MatchContext {
  /// The shortlist score, 0-100. Not a proportion -- see [scoreAsPercent].
  final double score;

  /// One sentence written for the freelancer.
  final String? reason;

  /// Evidence that counted in their favour.
  final List<String> strengths;

  /// Skills the project wanted and they do not have.
  final List<String> gaps;

  MatchContext({
    required this.score,
    this.reason,
    List<String>? strengths,
    List<String>? gaps,
  })  : strengths = List.unmodifiable(strengths ?? const []),
        gaps = List.unmodifiable(gaps ?? const []);

  /// Returns null when the server sent no reasoning.
  ///
  /// Nullable rather than an object with empty everything, because "there is no
  /// reasoning" and "the reasoning is empty" are different states and only one
  /// of them should change what the screen draws.
  static MatchContext? fromJsonOrNull(Map<String, dynamic>? json) {
    if (json == null || json.isEmpty) return null;

    return MatchContext(
      score: asDouble(json['score']),
      reason: asOptString(json['reason']),
      strengths: asStringList(json['strengths']),
      gaps: asStringList(json['gaps']),
    );
  }

  /// The score as a whole-number percentage.
  ///
  /// The server sends 0-100. A screen that divided by 100 again would show
  /// "0.9%" for a strong match, so the guard below accepts either scale: a value
  /// of 1 or less is read as a proportion, everything else as already a
  /// percentage. Getting this wrong is silent -- the number just looks wrong.
  String get scoreAsPercent {
    final value = (score > 0 && score <= 1) ? score * 100 : score;
    return '${value.round()}%';
  }

  /// Whether there is anything worth rendering.
  ///
  /// A reason of "null" with two empty lists is a match the model produced no
  /// explanation for. Showing a panel that says nothing is worse than showing
  /// none, so this is the check the UI uses before it draws one.
  bool get hasContent =>
      (reason != null && reason!.trim().isNotEmpty) ||
      strengths.isNotEmpty ||
      gaps.isNotEmpty;
}

class InvitationService with ChangeNotifier {
  List<Invitation> _invitations = [];
  List<Invitation> get invitations => List.unmodifiable(_invitations);

  bool _isLoading = false;
  bool get isLoading => _isLoading;

  /// Set when the last accept/decline was rejected by the server, so the list
  /// view can show it. Cleared on the next successful write.
  String? _lastError;
  String? get lastError => _lastError;

  final NetworkApiServices _apiService = NetworkApiServices();

  Future<void> fetchInvitations({String? status}) async {
    _isLoading = true;
    notifyListeners();

    try {
      final query = <String, String>{};
      if (status != null && status.isNotEmpty) {
        // `EnumQueryParser.TryParse` on the server is case-insensitive over the
        // snake_case names, so the wire value round-trips.
        query['status'] = InvitationStatus.normalize(status);
      }
      final qs = query.isEmpty
          ? ''
          : '?${query.entries.map((e) => '${e.key}=${e.value}').join('&')}';
      final response = await _apiService.getApi(
          '${AppUrls.receivedInvitationsUrl}$qs', LocalKeys.invitations,
          headers: commonAuthHeader);

      if (response is List) {
        // Tolerate a bare array: the paged and unpaged services both exist
        // behind these two routes, and `GetReceivedInvitations` (unpaged) is
        // still reachable on `/api/invitations/received`.
        _invitations = _parseList(response);
      } else if (response is Map) {
        // Paged envelope: `{count, next, previous, results}`. The previous
        // version read `response['results'] ?? response['data']` and then guarded
        // with `if (data is List)` -- but `data` is a `dynamic`, so the guard
        // was the only thing standing between a 200 and a type error. It
        // happened to work; the `as List` cast below is what actually broke on
        // a null `results`.
        _invitations = _parseList(response['results'] ?? response['data']);
      } else {
        // null == failure. Leave the previous list alone rather than blanking
        // the screen on a flaky connection.
        return;
      }
      _lastError = null;
    } catch (e) {
      debugPrint('Error fetching invitations: $e');
    } finally {
      _isLoading = false;
      notifyListeners();
    }
  }

  static List<Invitation> _parseList(dynamic raw) {
    if (raw is! List) return <Invitation>[];
    return raw
        .whereType<Map>()
        .map((e) => Invitation.fromJson(Map<String, dynamic>.from(e)))
        .toList();
  }

  /// `PATCH /api/projects/invitations/{id}/update-status` body
  /// `{"status": "accepted"}` -> **204 No Content**.
  ///
  /// The previous version built the URL with
  /// `receivedInvitationsUrl.replaceAll("received", "")` -- a string edit that
  /// only produced the right path by coincidence -- passed a hand-built JSON
  /// **string** instead of a `Map`, sent `Accepted`/`Declined` in PascalCase
  /// where the server's `JsonStringEnumConverter(SnakeCaseLower)` expects
  /// `accepted`/`declined`, and then **threw the response away**: the local row
  /// was rewritten unconditionally, inside the `try`, so a 400 or a 403 still
  /// left the UI showing "Accepted" for an invitation the server had not
  /// changed. `patchApi` returns an empty map for 204 and `null` for any
  /// failure, so `!= null` is the honest success test.
  Future<bool> updateStatus(int id, String newStatus) async {
    final wire = InvitationStatus.normalize(newStatus);
    if (wire == InvitationStatus.cancelled) {
      // Freelancers cannot cancel -- the server rejects it with
      // 400 "Freelancers cannot cancel invitations."
      _lastError = LocalKeys.cannotCancelInvitation;
      notifyListeners();
      return false;
    }

    final index = _invitations.indexWhere((i) => i.id == id);
    final previous = index != -1 ? _invitations[index] : null;

    // Optimistic: flip the row so the buttons disable immediately.
    if (previous != null && previous.isActionable) {
      _invitations[index] = previous.copyWith(status: wire);
      notifyListeners();
    }

    final response = await _apiService.patchApi(
      {'status': wire},
      AppUrls.invitationStatusUrl(id),
      LocalKeys.invitations,
      headers: commonAuthHeader,
    );

    final succeeded = response != null;
    if (!succeeded && previous != null) {
      // Roll back to whatever the server last told us.
      _invitations[index] = previous;
      _lastError = LocalKeys.invitationUpdateFailed;
    } else {
      _lastError = null;
    }
    notifyListeners();
    return succeeded;
  }

  /// Re-reads one invitation's state from the server.
  ///
  /// `UpdateInvitationStatusAsync` is not fully idempotent -- it enforces the
  /// state machine -- so a rejected write and a lost response look identical
  /// locally. This is how the app tells them apart after the fact.
  Future<void> syncInvitation(int id) async {
    final response = await _apiService.getApi(
        AppUrls.receivedInvitationsUrl, LocalKeys.invitations,
        headers: commonAuthHeader);
    if (response is! Map) return;
    final fresh = _parseList(response['results'] ?? response['data']);
    if (fresh.isEmpty) return;

    final index = _invitations.indexWhere((i) => i.id == id);
    final match =
        fresh.firstWhere((f) => f.id == id, orElse: () => Invitation.fromJson(const {}));
    if (index != -1) {
      _invitations[index] = match;
    } else {
      _invitations.add(match);
    }
    notifyListeners();
  }

  void clear() {
    _invitations = <Invitation>[];
    _lastError = null;
    notifyListeners();
  }

  void showLastError() {
    final message = _lastError;
    if (message != null && message.isNotEmpty) message.showToast();
  }
}
