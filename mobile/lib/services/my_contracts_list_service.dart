import 'package:flutter/material.dart';

import '../../helper/constant_helper.dart';
import '../../helper/extension/string_extension.dart';
import '../../helper/local_keys.g.dart';
import '../../helper/query_string.dart';
import '../../models/contract_model.dart';
import '../data/network/network_api_services.dart';
import '../helper/app_urls.dart';

/// The freelancer's contracts -- the screen the template called "My Orders".
///
/// Backed by `GET /api/contracts/`, which returns
/// `{count, next, previous, results}`. That envelope is deliberate
/// (`ClientContracts.jsx` reads it), so paging follows `next`.
///
/// What was wrong before:
///  * The payload was parsed by a template `Order` model that read `price`,
///    `job`, `user.first_name` and ~25 other fields that do not exist on
///    `ContractResponse`, so every row rendered blank.
///  * `nextPageUrl` was read from `next_page_url`, a Laravel key this API never
///    sends, so [fetchNextPage] could only ever have fired with a null URL.
///    Pagination was silently dead -- page 2 of contracts was unreachable.
///  * `totalCount` was read from `total_count`; the real key is `count`.
class MyContractsListService with ChangeNotifier {
  final List<Contract> _contracts = [];
  String? _next;
  bool _fetched = false;
  bool _loading = false;
  String _token = '';

  /// The active status tab (`?status=` on `GET /api/contracts`), null = all.
  String? _status;

  /// The search box (`?search=`), matched against the project title.
  String _search = '';

  /// The contracts accumulated so far.
  List<Contract> get orderList => _contracts;

  String? get status => _status;
  String get search => _search;

  bool get isEmpty => _contracts.isEmpty;

  bool get isLoading => _loading;

  /// True while a *subsequent* page is in flight (the first load uses
  /// [isLoading]).
  bool get nextPageLoading => _nextPageLoading;
  bool _nextPageLoading = false;

  bool get nexLoadingFailed => _nextLoadingFailed;
  bool _nextLoadingFailed = false;

  /// True when the server reported another page. Drives the trailing spinner.
  ///
  /// The list previously gated its pagination affordance on a `nextPage` field
  /// that was read from `next_page_url` -- a Laravel key this API never sends --
  /// so it was permanently null. Paging silently never happened.
  bool get hasMore => (_next ?? '').isNotEmpty;

  /// Total reported by the server for the whole query, not just this page.
  int totalCount = 0;

  /// Re-fetch only when nothing is loaded yet, or the stored JWT no longer
  /// matches the current one (i.e. the user re-signed in as someone else).
  bool get shouldAutoFetch => !_fetched || _token.isInvalid;

  Future<void> fetchOrderList({String? status, String? search}) async {
    _token = getToken;
    if (_loading) return;
    _loading = true;
    notifyListeners();

    // A pull-to-refresh (no args) keeps the currently active tab and search.
    final resolvedStatus = status ?? _status;
    final resolvedSearch = search ?? _search;

    final params = <String, String>{};
    if (resolvedStatus != null && resolvedStatus.isNotEmpty) {
      params['status'] = resolvedStatus;
    }
    if (resolvedSearch.trim().isNotEmpty) {
      params['search'] = resolvedSearch.trim();
    }
    final url = params.isEmpty
        ? AppUrls.contractsUrl
        : '${AppUrls.contractsUrl}?${buildQueryString(params)}';

    final responseData = await NetworkApiServices()
        .getApi(url, LocalKeys.orderList, headers: commonAuthHeader);

    _loading = false;

    final page = ContractPage.fromJson(responseData);
    if (responseData != null) {
      // Replace, not append: a refresh must not duplicate rows.
      _contracts
        ..clear()
        ..addAll(page.results);
      _next = page.next;
      totalCount = page.count > 0 ? page.count : page.results.length;
    } else {
      _contracts.clear();
      _next = null;
      totalCount = 0;
    }
    _fetched = true;
    notifyListeners();
  }

  /// Follows the server's `next` link. No-ops when the last page was reached.
  Future<void> fetchNextPage() async {
    _token = getToken;
    final next = _next;
    if (next == null || next.isEmpty || _nextPageLoading) return;

    _nextPageLoading = true;
    _nextLoadingFailed = false;
    notifyListeners();

    final responseData = await NetworkApiServices()
        .getApi(next, LocalKeys.orderList, headers: commonAuthHeader);

    _nextPageLoading = false;

    final page = ContractPage.fromJson(responseData);
    if (responseData != null) {
      // Guard against a server that echoes a row already on screen.
      final seen = _contracts.map((c) => c.id).toSet();
      _contracts.addAll(page.results.where((c) => seen.add(c.id)));
      _next = page.next;
      totalCount = page.count > 0 ? page.count : _contracts.length;
    } else {
      // Surface the failure for a beat so the list can show a retry affordance,
      // then clear it.
      _nextLoadingFailed = true;
      Future.delayed(const Duration(seconds: 1), () {
        _nextLoadingFailed = false;
        notifyListeners();
      });
    }
    notifyListeners();
  }

  /// Switches the status tab and restarts from page 1.
  void setStatus(String? value) {
    if (_status == value) return;
    _status = value;
    fetchOrderList(status: value);
  }

  /// Applies the search box (submitted, not per-keystroke) and refetches.
  void setSearch(String value) {
    if (_search == value) return;
    _search = value;
    fetchOrderList(search: value);
  }

  /// Keeps the cached list consistent with an action taken on the detail screen
  /// (accept / cancel / submit) without a full round trip.
  void applyLocalStatus(int contractId, String status) {
    final index = _contracts.indexWhere((c) => c.id == contractId);
    if (index == -1) return;
    _contracts[index] = _contracts[index].copyWith(status: status);
    notifyListeners();
  }

  void clear() {
    _contracts.clear();
    _next = null;
    totalCount = 0;
    _fetched = false;
    _status = null;
    _search = '';
    notifyListeners();
  }
}
