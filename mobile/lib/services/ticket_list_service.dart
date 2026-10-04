import 'package:flutter/material.dart';
import 'package:webofwork/helper/extension/string_extension.dart';
import 'package:webofwork/helper/local_keys.g.dart';
import 'package:webofwork/models/ticket_list_model.dart';

import '../data/network/network_api_services.dart';
import '../helper/app_urls.dart';
import '../helper/constant_helper.dart';

/// `GET /api/support/tickets?page=&pageSize=` and
/// `PATCH /api/support/tickets/{id}/cancel`.
///
/// The **department** step was removed here and from the create screen.
/// `fetchDepartments()` called `AppUrls.stDepartmentsUrl` — a route that has
/// never existed on this API — so the request always 404'd and the
/// `departments == null` guard in `SupportTicketViewModel.createTicket` blocked
/// submission every time ("Select department", forever). `CreateSupportTicketRequest`
/// has exactly two fields, `Subject` and `Message`; there is no department,
/// priority or title on the wire.
class TicketListService with ChangeNotifier {
  List<Ticket> ticketList = [];

  /// Surface for "You have reached the maximum limit of N active support
  /// tickets" — the API returns 403 with that detail when the user has too many
  /// open tickets.
  int openCount = 0;
  int totalCount = 0;
  int totalPages = 0;

  int _page = 1;
  static const int pageSize = 20;
  bool get hasMore => _page < totalPages;

  var token = "";
  var nextPage;

  bool nextPageLoading = false;

  bool nexLoadingFailed = false;

  bool get shouldAutoFetch => ticketList.isEmpty || token.isInvalid;

  Future<void> fetchTicketList() async {
    token = getToken;
    _page = 1;
    final responseData = await NetworkApiServices().getApi(
        '${AppUrls.ticketListUrl}?page=$_page&pageSize=$pageSize',
        LocalKeys.supportTicket,
        headers: commonAuthHeader);

    if (responseData is Map) {
      final page = TicketListModel.fromJson(Map<String, dynamic>.from(responseData));
      ticketList = page.data;
      openCount = page.openCount;
      totalCount = page.totalCount;
      totalPages = page.totalPages;
      _page = page.page;
      // No cursor URL on this endpoint.
      nextPage = null;
    }
    notifyListeners();
  }

  /// Loads the next page number. The endpoint is `?page=&pageSize=`, not a
  /// `next` link, so the previous `getApi(nextPage, …)` could only ever have
  /// fired with `nextPage == null`.
  fetchNextPage() async {
    if (!hasMore) return;
    if (nextPageLoading) return;
    nextPageLoading = true;
    final next = _page + 1;
    final responseData = await NetworkApiServices().getApi(
        '${AppUrls.ticketListUrl}?page=$next&pageSize=$pageSize', LocalKeys.supportTicket,
        headers: commonAuthHeader);

    if (responseData is Map) {
      final page = TicketListModel.fromJson(Map<String, dynamic>.from(responseData));
      ticketList.addAll(page.data);
      totalCount = page.totalCount;
      openCount = page.openCount;
      totalPages = page.totalPages;
      _page = page.page;
    } else {
      nexLoadingFailed = true;
      Future.delayed(const Duration(seconds: 1)).then((value) {
        nexLoadingFailed = false;
        notifyListeners();
      });
    }
    nextPageLoading = false;
    notifyListeners();
  }

  /// `PATCH /api/support/tickets/{id}/cancel` -> the updated ticket.
  ///
  /// Only `open` tickets can be cancelled; the API returns 400
  /// ("Only open support tickets can be cancelled.") for anything else, which
  /// the network layer surfaces.
  Future<bool> cancelTicket(int id) async {
    final responseData = await NetworkApiServices().patchApi(
        {}, AppUrls.cancelTicketUrl(id), LocalKeys.supportTicket,
        headers: commonAuthHeader);

    if (responseData is Map) {
      final updated = Ticket.fromJson(
          Map<String, dynamic>.from(responseData['data'] ?? responseData));
      final index = ticketList.indexWhere((t) => t.id == id);
      if (index != -1) {
        ticketList[index] = updated;
        if (openCount > 0) openCount--;
        notifyListeners();
      }
      return true;
    }
    return false;
  }

  /// Re-reads a single ticket in place after a cancel, so a stale list cannot
  /// keep showing a live "Cancel" button.
  void syncTicket(Ticket updated) {
    final index = ticketList.indexWhere((t) => t.id == updated.id);
    if (index == -1) return;
    ticketList[index] = updated;
    notifyListeners();
  }
}
