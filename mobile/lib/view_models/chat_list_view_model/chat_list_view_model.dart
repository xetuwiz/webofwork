import 'package:flutter/material.dart';

/// Scroll plumbing for the inbox.
///
/// `tryLoadingMore` and the `nextPage` / `nextPageLoading` notifiers were
/// removed: `GET /api/chat/rooms` has **no** paging envelope and returns every
/// room in one bare `data` array, so `nextPage` was permanently null and
/// `fetchNextPage()` could only ever have fired with a null URL. The list screen
/// now re-fetches on overscroll instead of pretending to page.
class ChatListViewModel {
  final ScrollController scrollController = ScrollController();

  ChatListViewModel._init();
  static ChatListViewModel? _instance;
  static ChatListViewModel get instance {
    _instance ??= ChatListViewModel._init();
    return _instance!;
  }
}
