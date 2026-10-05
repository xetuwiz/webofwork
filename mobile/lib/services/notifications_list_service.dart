import 'package:flutter/material.dart';
import 'package:provider/provider.dart';
import 'package:webofwork/services/message_notification_count_service.dart';
import '../../helper/constant_helper.dart';
import '../../helper/extension/string_extension.dart';
import '../../helper/local_keys.g.dart';
import '../../models/notification_list_model.dart';

import '../data/network/network_api_services.dart';
import '../helper/app_urls.dart';

class NotificationsListService with ChangeNotifier {
  NotificationListModel? _notificationsList;
  NotificationListModel get notificationsList =>
      _notificationsList ?? NotificationListModel();
  var token = "";
  bool loading = false;

  var nextPage;

  bool nextPageLoading = false;
  bool nexLoadingFailed = false;

  /// `GET /api/notifications` takes `?isRead=&limit=` but has no page/`next`,
  /// so there is no page 2 to fetch. Kept because the scroll view still asks,
  /// but it will stay null.
  bool get hasMore => nextPage != null;

  /// Pushes that arrived over the socket before this list was ever loaded.
  ///
  /// The notification socket is opened as soon as the shell mounts, so pushes
  /// routinely land long before the user opens the notifications screen. They
  /// cannot be rendered into a list that does not exist yet, so they are held
  /// here and merged into the next fetch. Without the buffer the row vanishes
  /// from the app for as long as the user stays off that screen and then
  /// reappears, which reads as a glitch.
  final List<AppNotification> _pendingPushes = [];

  bool get shouldAutoFetch => _notificationsList == null || token.isInvalid;

  /// Seeds only the badge: loads the envelope, publishes `data.unread_count`,
  /// and deliberately does **not** mark anything read.
  ///
  /// This API has no unread-count route -- the previous implementation called
  /// `GET /api/notifications/unread-count/`, a guaranteed 404 -- so the envelope
  /// is the only source of the number. It must not be this *list* fetch that
  /// reads it, because that one marks everything read on first load: the badge
  /// would be set and cleared in the same breath on every cold start and the
  /// user would never see a count at all.
  Future<void> seedBadge(BuildContext context) async {
    final responseData = await NetworkApiServices().getApi(
        AppUrls.notificationsListUrl, LocalKeys.notifications,
        headers: commonAuthHeader);

    if (responseData == null) return;
    final model = NotificationListModel.fromJson(Map<String, dynamic>.from(responseData));

    final counts = Provider.of<MessageNotificationCountService>(context, listen: false);
    if (counts.notificationCount != model.unreadCount) {
      counts.setNC(model.unreadCount);
    }
  }

  /// Folds a row pushed over the notification socket into the loaded list.
  ///
  /// Returns true when the row was new, so the caller knows to raise the badge.
  /// A push for a row already in the list is dropped: the server commits the row
  /// *before* pushing it, so a push can arrive while a fetch for that same row
  /// is still in flight, and without this the row rendered twice.
  bool applyPush(AppNotification row) {
    final model = _notificationsList;
    if (model == null) {
      if (_pendingPushes.every((p) => p.id != row.id)) {
        _pendingPushes.add(row);
      }
      return true;
    }

    final list = model.notifications;
    if (list == null) {
      model.notifications = [row];
    } else {
      if (list.any((n) => n.id == row.id)) return false;
      list.insert(0, row);
    }

    if (!row.isRead) model.unreadCount += 1;
    model.totalCount += 1;
    notifyListeners();
    return true;
  }

  /// `Future<void>` (not implicit `Future<dynamic>`): the untyped return made
  /// the `() => service.fetchNotificationsList(context)` closure a
  /// `() => Future<dynamic>`, which the runtime rejects when it is downcast to
  /// `RefreshIndicator`'s `Future<void> Function()` — "type '() => dynamic' is
  /// not a subtype of type '() => Future<void>'".
  Future<void> fetchNotificationsList(BuildContext context) async {
    token = getToken;
    final responseData = await NetworkApiServices().getApi(
        AppUrls.notificationsListUrl, LocalKeys.notifications,
        headers: commonAuthHeader);

    if (responseData != null) {
      // The envelope is `{data: {unread_count, total_count, items}}`. This used
      // to read `responseData["notifications"]`, which is never present, so the
      // `?? {}` branch always won and the list rendered empty on a 200.
      _notificationsList =
          NotificationListModel.fromJson(Map<String, dynamic>.from(responseData));
      nextPage = null;

      // Merge anything that was pushed before this load. The server's response
      // normally already contains these rows -- the push is sent after the
      // insert commits -- so the id check is what stops them appearing twice,
      // and the merge is what covers the race where the fetch was issued before
      // the row was committed.
      final list = _notificationsList!.notifications;
      if (list != null) {
        for (final pending in _pendingPushes) {
          if (list.every((n) => n.id != pending.id)) list.insert(0, pending);
        }
      } else if (_pendingPushes.isNotEmpty) {
        _notificationsList!.notifications = [..._pendingPushes];
      }
      _pendingPushes.clear();

      notifyListeners();

      // Push the badge count out. `unread_count` in this response is the only
      // place the number exists -- there is no unread-count route on the API,
      // so the old /api/notifications/unread-count/ request was a guaranteed
      // 404 and the badge never moved off zero.
      Provider.of<MessageNotificationCountService>(context, listen: false)
          .setNC(_notificationsList!.unreadCount);
    }
    notifyListeners();
  }

  Future<void> fetchNextPage(BuildContext context) async {
    if (nextPage == null) return;
    token = getToken;
    if (nextPageLoading) return;
    nextPageLoading = true;
    final responseData = await NetworkApiServices()
        .getApi(nextPage, LocalKeys.notifications, headers: commonAuthHeader);

    if (responseData != null) {
      final tempData =
          NotificationListModel.fromJson(Map<String, dynamic>.from(responseData));
      tempData.notifications?.forEach((element) {
        _notificationsList?.notifications?.add(element);
      });
      nextPage = null;
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

  /// `PATCH /api/notifications/read-all` -> `{"marked": n}`.
  ///
  /// This was a `postApi` to `/api/notifications/mark-all-read/`, which is
  /// neither the right verb nor a real path. Wired to the AppBar action; the
  /// list no longer marks everything read on first load, because that made the
  /// badge vanish before the user saw any of it.
  Future<void> markAllRead(BuildContext context) async {
    final responseData = await NetworkApiServices().patchApi(
        {}, AppUrls.markAllReadUrl, LocalKeys.notifications,
        headers: commonAuthHeader);
    if (responseData != null) {
      // Clear locally too: the rows keep their old `is_read` in memory, so
      // without this the unread styling stayed until a cold start.
      for (final n in _notificationsList?.notifications ?? <AppNotification>[]) {
        n.isRead = true;
      }
      _notificationsList?.unreadCount = 0;
      _pendingPushes.clear();
      Provider.of<MessageNotificationCountService>(context, listen: false).setNC(0);
      notifyListeners();
    }
  }

  /// `PATCH /api/notifications/{id}/read` -> marks one row read.
  ///
  /// Fired from the list tile (mark on open, and via the row's mark-read
  /// action). The local row is updated only on a non-null response, so a failed
  /// call does not pretend the server agreed.
  Future<void> markRead(BuildContext context, AppNotification n) async {
    if (n.isRead) return;
    final responseData = await NetworkApiServices().patchApi(
        {}, AppUrls.notificationReadUrl(n.id), LocalKeys.notifications,
        headers: commonAuthHeader);
    if (responseData != null) {
      n.isRead = true;
      final model = _notificationsList;
      if (model != null && model.unreadCount > 0) {
        model.unreadCount -= 1;
      }
      Provider.of<MessageNotificationCountService>(context, listen: false)
          .setNC(model?.unreadCount ?? 0);
      notifyListeners();
    }
  }
}
