import 'package:flutter/material.dart';

/// Holds the two badge numbers shown in the UI.
///
/// There is deliberately **no fetch method here**. The previous version called
/// `GET /api/chat/unread-count/` and `GET /api/notifications/unread-count/`;
/// neither route exists on this API, so both requests 404'd and both counters
/// stayed at 0 forever. The real sources are:
///
///  * notifications -- `data.unread_count` in the `GET /api/notifications`
///    envelope, pushed in by `NotificationsListService`.
///  * messages -- summed `unread_count` across the rooms in
///    `GET /api/chat/rooms`, pushed in by the chat list service.
class MessageNotificationCountService with ChangeNotifier {
  num messageCount = 0;
  num notificationCount = 0;

  setMC(value) {
    if (value == messageCount) {
      return;
    }
    messageCount = value;
    notifyListeners();
  }

  setNC(value) {
    if (value == notificationCount) {
      return;
    }
    notificationCount = value;
    notifyListeners();
  }

  /// Used when the user pulls to refresh, where the real list fetch will push a
  /// fresh count in anyway -- resets the badge optimistically so the number does
  /// not visibly lag the refresh.
  resetCounts() {
    messageCount = 0;
    notificationCount = 0;
    notifyListeners();
  }
}
