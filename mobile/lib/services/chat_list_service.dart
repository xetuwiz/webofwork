import 'package:flutter/material.dart';
import 'package:webofwork/helper/app_urls.dart';
import 'package:webofwork/helper/constant_helper.dart';
import 'package:webofwork/helper/extension/string_extension.dart';
import 'package:webofwork/helper/local_keys.g.dart';
import 'package:webofwork/services/message_notification_count_service.dart';

import '../data/network/network_api_services.dart';
import '../models/chat_list_model.dart';

/// Room list from `GET /api/chat/rooms` -> `{ data: [ ChatRoomResponse ] }`.
///
/// Also owns the *message* badge: there is no unread-count endpoint on this API,
/// so the total is the sum of `unread_count` across the rooms, pushed into
/// [MessageNotificationCountService] on every successful fetch.
class ChatListService with ChangeNotifier {
  ChatListModel? _chatListModel;

  ChatListModel get chatListModel => _chatListModel ?? ChatListModel();

  var token = "";

  /// Newest conversation first.
  List<Chats> get rooms {
    final list = [...?chatListModel.chatList?.chats];
    list.sort((a, b) {
      final at = a.lastMessageAt ?? a.createdAt ?? DateTime(1970);
      final bt = b.lastMessageAt ?? b.createdAt ?? DateTime(1970);
      return bt.compareTo(at);
    });
    return list;
  }

  bool get shouldAutoFetch => _chatListModel == null || token.isInvalid;

  Future<void> fetchChatList() async {
    token = getToken;
    final responseData = await NetworkApiServices()
        .getApi(AppUrls.chatListUrl, LocalKeys.inbox, headers: acceptJsonAuthHeader);

    if (responseData != null) {
      _chatListModel = ChatListModel.fromJson(responseData);
      notifyListeners();
    }
  }

  /// Publishes a freshly-received message into the room row it belongs to, so
  /// the list stays live while a different room is open.
  void applyIncomingMessage(int roomId, String preview, DateTime at) {
    final chats = _chatListModel?.chatList?.chats;
    if (chats == null) return;
    Chats? target;
    for (final c in chats) {
      if (c.id == roomId) {
        target = c;
        break;
      }
    }
    if (target == null) return;
    target.lastMessagePreview = preview;
    target.lastMessageAt = at;
    target.unreadCount = target.unreadCount + 1;
    notifyListeners();
  }

  /// Called when a room is opened. Clears the badge locally and asks the server
  /// to do the same (`PATCH /api/chat/rooms/{id}/read`).
  void setChatRead(id) {
    final chats = _chatListModel?.chatList?.chats;
    if (chats == null) return;
    for (final c in chats) {
      if (c.id.toString() == id.toString()) {
        c.unreadCount = 0;
        break;
      }
    }
    notifyListeners();
  }

  void markRoomReadOnServer(int roomId, {int? messageId}) {
    NetworkApiServices().patchApi(
      {if (messageId != null) 'message_id': messageId},
      AppUrls.chatRoomReadUrl(roomId),
      LocalKeys.message,
      headers: acceptJsonAuthHeader,
    );
  }

  /// Bump the aggregate message badge without refetching the whole list.
  void bumpMessageCount(int roomId) {
    final chats = _chatListModel?.chatList?.chats;
    for (final c in chats ?? const <Chats>[]) {
      if (c.id == roomId) {
        c.unreadCount = c.unreadCount + 1;
        break;
      }
    }
    notifyListeners();
  }

  /// The chat room for a contract, creating it if the server has not yet.
  ///
  /// `POST /api/chat/rooms` body `{ "contract_id": <int> }` -> `{ "data":
  /// ChatRoomResponse }`. This is the only way in from the contract screen, and
  /// it was previously unreachable: the button posted
  /// `{ "contractId": … }` (camelCase, which the snake_case binder drops) to
  /// this same route, then pushed `ConversationView` with a 5-element list of
  /// positional arguments. `ConversationView` has since been rebuilt to take a
  /// single `Chats` object via `context.arguments`, so a list could no longer
  /// have been read even if the request had succeeded.
  ///
  /// The created room is merged into the cached list, so returning to the inbox
  /// shows the new conversation without a refetch.
  Future<Chats?> getOrCreateRoomForContract(int contractId) async {
    if (contractId <= 0) return null;

    final responseData = await NetworkApiServices().postApi(
      {'contract_id': contractId},
      AppUrls.chatGetOrCreateUrl,
      LocalKeys.inbox,
      headers: acceptJsonAuthHeader,
    );

    if (responseData is! Map) return null;

    // `{ "data": { ... } }` on create. Tolerating a bare `ChatRoomResponse` costs
    // nothing and covers the case where the wrapper is ever dropped.
    final data = responseData['data'];
    final raw = data is Map
        ? Map<String, dynamic>.from(data)
        : Map<String, dynamic>.from(responseData);

    final room = Chats.fromJson(raw);
    if (room.id == null) return null;

    final chats = _chatListModel?.chatList?.chats;
    if (chats != null) {
      final existing = chats.indexWhere((c) => c.id == room.id);
      if (existing >= 0) {
        chats[existing] = room;
      } else {
        chats.add(room);
      }
      notifyListeners();
    }
    return room;
  }
}
