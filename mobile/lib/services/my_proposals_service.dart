import 'package:flutter/foundation.dart';

import '../../data/network/network_api_services.dart';
import '../../helper/app_urls.dart';
import '../../helper/constant_helper.dart';
import '../../helper/extension/string_extension.dart';
import '../../helper/local_keys.g.dart';
import '../../models/my_proposals_model.dart';

/// The freelancer's submitted proposals.
///
/// Three independent bugs made this list permanently empty:
///
///  * **Wrong route.** [AppUrls.myProposalsUrl] pointed at `GET /api/proposals/`.
///    `ProposalsController` (`[Route("api/[controller]")]`) only exposes GET on
///    `my` and `project/{projectId}` -- there is no GET on the bare prefix, so
///    the request 404'd. The web client uses the absolute alias
///    `/api/projects/my-proposals/`, which is what the URL now points at.
///  * **Wrong envelope.** The old model read `json["my_proposals"]`; the endpoint
///    answers `{count, next, previous, results}`.
///  * **Wrong fields.** `amount`/`duration` and five `is_*` flags the server
///    never sends, so even a matching envelope would have rendered blanks.
///
/// Pagination is real on this endpoint (`next` is a relative `?page=N`), which
/// makes it one of the few list screens that genuinely pages.
class MyProposalsService with ChangeNotifier {
  final List<MyProposal> _proposals = [];
  String? _next;
  String _token = '';
  bool _loading = false;
  bool _nextPageLoading = false;
  bool _nextLoadingFailed = false;

  /// The active status tab: null means "all". Mirrored on the server as
  /// `?status=` (`ProposalStatus` via `Enum.TryParse`, unknown values ignored).
  String? _status;

  /// The proposals accumulated so far.
  List<MyProposal> get proposalList => List.unmodifiable(_proposals);

  String? get status => _status;

  bool get isLoading => _loading;
  bool get isEmpty => _proposals.isEmpty;
  bool get nextPageLoading => _nextPageLoading;
  bool get nexLoadingFailed => _nextLoadingFailed;

  /// True while a *subsequent* page is in flight.
  bool get hasMore => (_next ?? '').isNotEmpty;

  /// Total for the whole query, not just the loaded pages.
  int totalCount = 0;

  bool get shouldAutoFetch => _proposals.isEmpty || _token.isInvalid;

  /// Switches the status tab and restarts from page 1.
  void setStatus(String? value) {
    if (_status == value) return;
    _status = value;
    fetchOrderList(status: value);
  }

  /// Restarts from page 1. Any status filter is passed straight through; the
  /// server parses it with `Enum.TryParse<ProposalStatus>`, so an unknown value
  /// is ignored rather than 400ing. A pull-to-refresh (no argument) keeps the
  /// currently selected tab via [_status].
  Future<void> fetchOrderList({String? status}) async {
    if (_loading) return;
    _loading = true;
    _token = getToken;
    notifyListeners();

    final url = AppUrls.myProposalsUrlWithQuery(page: 1, status: status ?? _status);
    final responseData = await NetworkApiServices()
        .getApi(url, LocalKeys.myProposals, headers: commonAuthHeader);

    if (responseData != null) {
      final page = MyProposalsPage.fromJson(responseData);
      _proposals
        ..clear()
        ..addAll(page.results);
      _next = page.next;
      totalCount = page.count;
    } else {
      _proposals.clear();
      _next = null;
      totalCount = 0;
    }

    _loading = false;
    notifyListeners();
  }

  /// Appends the next page, following the `next` query string the server sent.
  ///
  /// `next` is relative (`?page=2&pageSize=10`), so it is resolved against the
  /// endpoint rather than passed to [getApi] as-is -- the previous code handed
  /// the bare `?page=2` straight to `getApi`, which is not a URL.
  fetchNextPage() async {
    if (nextPageLoading) return;
    if (!hasMore) return;
    _nextPageLoading = true;
    _token = getToken;
    notifyListeners();

    final url = AppUrls.myProposalsUrlWithQuery(next: _next);
    final responseData = await NetworkApiServices()
        .getApi(url, LocalKeys.myProposals, headers: commonAuthHeader);

    if (responseData != null) {
      final page = MyProposalsPage.fromJson(responseData);
      _proposals.addAll(page.results);
      _next = page.next;
      if (page.count > 0) totalCount = page.count;
    } else {
      _nextLoadingFailed = true;
      Future.delayed(const Duration(seconds: 1)).then((_) {
        _nextLoadingFailed = false;
        notifyListeners();
      });
    }

    _nextPageLoading = false;
    notifyListeners();
  }

  /// Drops a proposal the user has just withdrawn.
  void removeProposal(int id) {
    final before = _proposals.length;
    _proposals.removeWhere((p) => p.id == id);
    if (_proposals.length == before) return;
    if (totalCount > _proposals.length) totalCount = _proposals.length;
    notifyListeners();
  }

  /// `DELETE /api/proposals/{id}` -> **204 No Content**.
  ///
  /// Returns true only on success. `ProposalService.WithdrawProposalAsync`
  /// answers 403 for a proposal owned by someone else and 400 with
  /// `{ "error": "Only pending proposals can be withdrawn." }` for any status
  /// other than `Pending`, so neither is treated as a withdrawal here — the
  /// caller refetches instead, because both mean the cached row is stale.
  Future<bool> withdraw(int id) async {
    _token = getToken;
    final responseData = await NetworkApiServices().deleteApi(
      '${AppUrls.proposalDetailUrl}$id',
      LocalKeys.myProposals,
      headers: commonAuthHeader,
    );

    // `returnResponse` discards the status code for 200/201 and hands a
    // 204/205 back as an **empty map**, so "a response at all" is the honest
    // success test. A 4xx body is a non-empty map of error fields.
    if (responseData is Map && responseData.isNotEmpty) return false;

    removeProposal(id);
    return true;
  }
}
