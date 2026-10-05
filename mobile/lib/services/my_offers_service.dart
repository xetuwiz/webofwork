import 'package:flutter/foundation.dart';

import '../../data/network/network_api_services.dart';
import '../../helper/app_urls.dart';
import '../../helper/constant_helper.dart';
import '../../helper/extension/string_extension.dart';
import '../../helper/local_keys.g.dart';
import '../../models/contract_model.dart';

/// The freelancer's inbound offers: contracts with `status = offered`, waiting
/// for this freelancer to review and sign.
///
/// What was wrong before, in order of how badly it hurt:
///  * The payload was parsed by a private `MyOffersModel` that read
///    `json["my_offers"]`. [AppUrls.myOffersUrl] is
///    `GET /api/contracts?status=offered`, which answers
///    `{count, next, previous, results}` -- there is no `my_offers` key, so the
///    lookup was always null and **the list was permanently empty**. The screen
///    rendered "No offers yet" no matter how many were waiting.
///  * The `Offer` model read `price`, `deadline`, `client.first_name`,
///    `revision_left` and `attachment`, none of which `ContractResponse` carries,
///    so even if the envelope had matched every field would have been empty.
///  * `nextPage` was read from `next_page_url`, a Laravel key this API never
///    sends, and `fetchNextPage` had no null guard -- it would have called
///    `getApi(null, ...)` had anything invoked it.
///
/// Now it parses through the shared [ContractPage]/[Contract] pair that the
/// contracts list already uses, so the two screens cannot drift apart again.
class MyOffersService with ChangeNotifier {
  final List<Contract> _offers = [];
  bool _loading = false;
  String _token = '';

  /// The offers currently held, oldest-first as the server returns them.
  List<Contract> get offerList => List.unmodifiable(_offers);

  bool get isLoading => _loading;

  bool get isEmpty => _offers.isEmpty;

  /// Total reported by the server for the whole query.
  int totalCount = 0;

  /// Which endpoint this actually reads, and why that matters.
  ///
  /// [AppUrls.myOffersUrl] is `GET /api/contracts?status=offered` -- that is
  /// `ContractsController.GetContracts`, the *paged* list -- and NOT the
  /// separate `GetMyOffers` action at `/api/contracts/my_offers`. The two are
  /// easy to confuse, and an earlier version of this comment got it backwards.
  ///
  /// For the record, since both have come up:
  ///  * `GetContracts` is paged and reports `next`/`previous`.
  ///  * `GetMyOffers` used to answer a bare array, so its own paginator was
  ///    dead (`.next` on an array is always undefined). It now returns the
  ///    standard `{count, next, previous, results}` envelope.
  ///
  /// This screen is unaffected by either change, because it calls neither
  /// `my_offers` nor any `/admin/` route, and because [ContractPage.fromJson]
  /// accepts a bare list as well as the envelope.
  ///
  /// `hasMore` is therefore driven by the reported `count` rather than by
  /// `next`, which is what makes it work against both shapes.
  bool get hasMore => _offers.length < totalCount;

  bool get shouldAutoFetch => _offers.isEmpty || _token.isInvalid;

  Future<void> fetchOrderList() async {
    if (_loading) return;
    _loading = true;
    _token = getToken;
    notifyListeners();

    final responseData = await NetworkApiServices()
        .getApi(AppUrls.myOffersUrl, LocalKeys.myOffers, headers: commonAuthHeader);

    if (responseData != null) {
      final page = ContractPage.fromJson(responseData);
      _offers
        ..clear()
        ..addAll(page.results);
      // The endpoint filters to `Status == Offered` server-side, but a row can
      // still be signed out from under us between the fetch and the render, so
      // the client narrows too rather than showing a stale "offered" chip on a
      // contract that is already active.
      _offers.removeWhere((c) => c.status != Contract.statusOffered);
      totalCount = page.count;
    } else {
      _offers.clear();
      totalCount = 0;
    }

    _loading = false;
    notifyListeners();
  }

  /// Drops an offer the user has just accepted or declined, so the list does not
  /// keep offering an action on a contract that has left the `offered` state.
  void removeOffer(int contractId) {
    final before = _offers.length;
    _offers.removeWhere((c) => c.id == contractId);
    if (_offers.length == before) return;
    if (totalCount > _offers.length) totalCount = _offers.length;
    notifyListeners();
  }
}
