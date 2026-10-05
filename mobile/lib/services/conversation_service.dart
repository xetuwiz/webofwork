import 'dart:async';

import 'package:audioplayers/audioplayers.dart';
import 'package:flutter/foundation.dart';
import 'package:flutter/material.dart';
import 'package:provider/provider.dart';
import 'package:webofwork/helper/app_urls.dart';
import 'package:webofwork/helper/constant_helper.dart';
import 'package:webofwork/helper/extension/string_extension.dart';
import 'package:webofwork/helper/local_keys.g.dart';
import 'package:webofwork/helper/web_socket_service.dart';
import 'package:webofwork/services/profile_info_service.dart';

import '../data/network/network_api_services.dart';
import '../models/conversation_model.dart';
import 'chat_list_service.dart';

/// A chat thread.
///
/// History comes over REST (`GET /api/chat/rooms/{id}/messages`); everything
/// after that is the WebSocket at `/ws/chat/{roomId}`:
///
///  * **send** -- prefers the socket (`{"type":"chat_message"}`), falls back to
///    `POST /api/chat/rooms/{id}/messages` with `{content}` when the socket is
///    down. The server broadcasts only to *other* sockets, so the sender's own
///    copy always comes from the HTTP response, not the socket.
///  * **typing** -- `{"type":"typing","is_typing":…}`, surfaced as [isPeerTyping].
///  * **read receipts** -- `{"type":"read_receipt","message_id":n}` flips that
///    message's [Datum.isRead].
///  * **delete** -- `{"type":"delete_message","message_id":n}` and the server's
///    `{"type":"delete","message_id":n}` broadcast tombstone it locally.
///  * **errors** -- surfaced verbatim, so "Sharing contact info is prohibited"
///    and "Contract ended" reach the user instead of vanishing.
///
/// The previous version sent over REST but parsed the reply into an optimistic
/// `Datum(fromUser: "2", ...)` while reading history from a model that matched
/// no key in the response, and drove "live" updates through a Pusher client that
/// never connected.
class ConversationService with ChangeNotifier {
  ConversationModel? _conversationModel;
  ConversationModel get conversationModel => _conversationModel ?? ConversationModel();

  var token = "";
  String id = "";

  int get roomId => int.tryParse(id) ?? 0;

  bool nextPageLoading = false;
  bool nexLoadingFailed = false;

  /// True while the room socket is connected; drives the "Reconnecting…" hint.
  bool _socketConnected = false;
  bool get isSocketConnected => _socketConnected;

  /// The **other** participant is typing (received over the socket).
  bool _peerIsTyping = false;
  bool get isPeerTyping => _peerIsTyping;

  /// What **we** last told the peer. Kept separate from [_peerIsTyping] so the
  /// de-dupe in `setPeerTyping` is not comparing the peer's state against our
  /// own outgoing flag -- with the shared field, a peer typing indicator
  /// appearing or clearing silently suppressed the next outgoing `typing`
  /// frame, and vice versa.
  bool _weAreTyping = false;

  /// Set once, so only the first fetch triggers the socket connect.
  bool _socketBound = false;
  StreamSubscription<SocketEvent>? _socketEvents;

  /// `ChangeNotifier` has no `mounted`; the old socket listener used one and
  /// failed to compile, so a late frame arriving after `dispose()` would have
  /// called `notifyListeners()` on a dead notifier.
  bool _disposed = false;

  /// Cursor for the next older page (`before_message_id`).
  int? _oldestLoadedId;

  /// Whether older messages remain on the server. Derived from the page's own
  /// `has_more`, or from the loaded-vs-total comparison once a second page has
  /// been merged in.
  bool get hasMore => _conversationModel?.allMessage?.hasMore ?? false;

  bool shouldAutoFetch(conversationId) =>
      conversationId.toString() != id.toString() || token.isInvalid;

  // ── History ─────────────────────────────────────────────────────────────────

  Future<bool> fetchConversationMessages(
    conversationId, {
    required BuildContext context,
  }) async {
    try {
      token = getToken;
      id = conversationId.toString();
      setConversationId(conversationId);
      final chatList = Provider.of<ChatListService>(context, listen: false);
      chatList.setChatRead(conversationId);
      _bindSocket(context, int.tryParse(conversationId.toString()) ?? 0);

      final url = '${AppUrls.chatMessagesUrl}$conversationId/messages';
      final responseData = await NetworkApiServices()
          .getApi(url, LocalKeys.message, headers: acceptJsonAuthHeader);

      if (responseData is Map) {
        _conversationModel = ConversationModel.fromJson(responseData);
        final items = _conversationModel?.allMessage?.data ?? const <Datum>[];
        // The list is oldest-first after the model reverses it, so the oldest
        // loaded id is the cursor for the *previous* page.
        _oldestLoadedId = items.isEmpty ? null : items.first.id;
        notifyListeners();
        return true;
      }
    } catch (e) {
      debugPrint('[conversation] fetch failed: $e');
    }
    return false;
  }

  /// Loads the page of messages *older* than the oldest one on screen.
  Future<void> fetchNextPage() async {
    if (nextPageLoading || !(_conversationModel?.allMessage?.hasMore ?? false)) {
      return;
    }
    final cursor = _oldestLoadedId;
    if (cursor == null) return;

    nextPageLoading = true;
    notifyListeners();

    final url = '${AppUrls.chatMessagesUrl}$roomId/messages'
        '?pageSize=50&beforeMessageId=$cursor';
    final responseData = await NetworkApiServices()
        .getApi(url, LocalKeys.message, headers: acceptJsonAuthHeader);

    if (responseData is Map) {
      final older =
          ConversationModel.fromJson(responseData).allMessage?.data?.reversed.toList() ??
              const <Datum>[];
      _conversationModel?.allMessage?.data?.insertAll(0, older);
      if (older.isNotEmpty) _oldestLoadedId = older.first.id;
      _conversationModel?.allMessage?.hasMore =
          (_conversationModel?.allMessage?.data?.length ?? 0) <
              (_conversationModel?.allMessage?.totalCount ?? 0);
    } else {
      nexLoadingFailed = true;
      Future.delayed(const Duration(seconds: 1)).then((_) {
        nexLoadingFailed = false;
        notifyListeners();
      });
    }
    nextPageLoading = false;
    notifyListeners();
  }

  // ── Socket plumbing ─────────────────────────────────────────────────────────

  void _bindSocket(BuildContext context, int room) {
    if (_socketBound) return;
    _socketBound = true;

    final socket = Provider.of<WebSocketService>(context, listen: false);
    socket.connectToRoom(room);

    _socketEvents = socket.events.listen((event) {
      if (_disposed) return;
      switch (event) {
        case ChatMessageEvent(:final message):
          _onIncoming(message);
        case TypingEvent(:final isTyping):
          _peerIsTyping = isTyping;
          notifyListeners();
        case ReadReceiptEvent(:final messageId):
          _markRead(messageId);
        case MessageDeletedEvent(:final messageId):
          _markDeleted(messageId);
        case AllReadEvent():
          for (final m in _conversationModel?.allMessage?.data ?? const <Datum>[]) {
            m.isRead = true;
          }
          notifyListeners();
        case SocketErrorEvent(:final message):
          // Business-rule rejections ("Sharing contact info is prohibited.",
          // "Contract ended.") used to disappear entirely.
          message.showToast();
        case NotificationPushEvent():
          break;
        case ConnectionStateEvent(:final connected):
          _socketConnected = connected;
          notifyListeners();
      }
    });
  }

  void _onIncoming(Map<String, dynamic> payload) {
    final datum = Datum.fromSocketMessage(payload);
    if (datum.id == null) return;
    // De-dupe: the socket can redeliver after a reconnect.
    final existing = _conversationModel?.allMessage?.data ?? const <Datum>[];
    if (existing.any((m) => m.id == datum.id)) return;

    existing.add(datum);
    _conversationModel?.allMessage?.hasMore = true;
    _playSound('audios/message_received.mp3');
    notifyListeners();
  }

  void _markRead(int messageId) {
    for (final m in _conversationModel?.allMessage?.data ?? const <Datum>[]) {
      if ((m.id ?? 0) <= messageId) m.isRead = true;
    }
    notifyListeners();
  }

  void _markDeleted(int messageId) {
    final list = _conversationModel?.allMessage?.data;
    if (list == null) return;
    final index = list.indexWhere((m) => m.id == messageId);
    if (index == -1) return;
    list[index].isDeleted = true;
    list[index].text = null;
    notifyListeners();
  }

  void _playSound(String asset) {
    // Best-effort: a missing asset or a locked audio session must not take down
    // the socket handler.
    unawaited(() async {
      try {
        await AudioPlayer().play(AssetSource(asset));
      } catch (_) {}
    }());
  }

  // ── Outbound ────────────────────────────────────────────────────────────────

  /// Sends over the socket when it is up, otherwise over REST. Returns false
  /// only when *both* fail, so the composer can re-enable the field.
  Future<bool> trySendingMessage(message) async {
    final text = (message ?? '').toString();
    if (text.trim().isEmpty) return false;

    final socket = _socketOrNull;
    if (socket != null && socket.sendChatMessage(text)) {
      // The sender is not echoed back over the socket, so the row is added from
      // the local echo here and reconciled by id on the next history fetch.
      _appendLocalEcho(text);
      _playSound('audios/message_sent.mp3');
      return true;
    }

    // Fallback: REST. `MessageRequest` binds `content` (snake_case of
    // `Content`), which is what this sends.
    final url = '${AppUrls.messageSendUrl}$roomId/messages';
    final responseData = await NetworkApiServices().postApi(
        {'content': text}, url, LocalKeys.sendMessage,
        headers: acceptJsonAuthHeader);

    if (responseData is Map) {
      final saved = responseData['data'];
      if (saved is Map) {
        _appendLocalEcho(text,
            id: saved['id'] is num ? (saved['id'] as num).toInt() : null);
      } else {
        _appendLocalEcho(text);
      }
      _playSound('audios/message_sent.mp3');
      notifyListeners();
      return true;
    }
    return false;
  }

  void _appendLocalEcho(String text, {int? id}) {
    // `a?.b ??= c` is not assignable when `a` is nullable, so the chain is
    // materialised in explicit steps.
    final model = _conversationModel ??= ConversationModel();
    final all = model.allMessage ??= AllMessage();
    final list = all.data ??= <Datum>[];
    if (id != null && list.any((m) => m.id == id)) return;
    list.add(Datum(
      id: id,
      roomId: roomId,
      senderId: _selfId,
      senderRole: 'freelancer',
      text: text,
      createdAt: DateTime.now(),
    ));
  }

  String? _selfIdOrNull;
  String? get _selfId => _selfIdOrNull;

  void adoptSelfUserId(String? id) => _selfIdOrNull = id;

  WebSocketService? _socketOrNull;

  /// Lets the view hand the service its socket instance without a `BuildContext`
  /// in the send path (which may run after the widget is gone).
  void attachSocket(WebSocketService socket) => _socketOrNull = socket;

  /// Tells the peer whether we are typing. De-duped against [_weAreTyping] so a
  /// keystroke burst produces one frame, not one per character.
  void setPeerTyping(bool value) {
    if (_weAreTyping == value) return;
    _weAreTyping = value;
    _socketOrNull?.sendTyping(value);
  }

  bool deleteMessage(int messageId) => _socketOrNull?.deleteMessage(messageId) ?? false;

  /// REST delete, used when the socket is unavailable.
  Future<bool> deleteMessageOverRest(int messageId) async {
    final url = '${AppUrls.chatMessagesUrl}$roomId/messages/$messageId';
    final responseData = await NetworkApiServices()
        .deleteApi(url, LocalKeys.message, headers: acceptJsonAuthHeader);
    if (responseData != null) {
      _markDeleted(messageId);
      return true;
    }
    return false;
  }

  @override
  void dispose() {
    _disposed = true;
    // Clear the typing flag on the way out, otherwise the peer's composer keeps
    // showing "…" for the rest of the session.
    if (_weAreTyping) {
      _weAreTyping = false;
      _socketOrNull?.sendTyping(false);
    }
    _socketEvents?.cancel();
    _socketEvents = null;
    _socketBound = false;
    _socketOrNull?.disconnectRoom();
    super.dispose();
  }
}

/// Convenience for reading the signed-in user id, which is what a message's
/// `sender_id` is compared against.
extension ConversationServiceProfile on ConversationService {
  String? selfIdFrom(BuildContext context) {
    try {
      return Provider.of<ProfileInfoService>(context, listen: false)
          .profileInfoModel
          .data
          ?.id;
    } catch (_) {
      return null;
    }
  }
}
