import 'dart:async';
import 'dart:convert';

import 'package:flutter/foundation.dart';
import 'package:web_socket_channel/web_socket_channel.dart';

import '../customizations.dart';
import 'constant_helper.dart';
import 'query_string.dart';

/// Typed events emitted by [WebSocketService].
sealed class SocketEvent {
  const SocketEvent();
}

/// A peer sent a message in the room we are attached to.
class ChatMessageEvent extends SocketEvent {
  final Map<String, dynamic> message;
  const ChatMessageEvent(this.message);
}

/// A peer started or stopped typing.
class TypingEvent extends SocketEvent {
  final String senderId;
  final bool isTyping;
  const TypingEvent(this.senderId, this.isTyping);
}

/// The peer read up to (and including) this message id.
class ReadReceiptEvent extends SocketEvent {
  final int messageId;
  const ReadReceiptEvent(this.messageId);
}

/// A message was deleted by its sender.
class MessageDeletedEvent extends SocketEvent {
  final int messageId;
  const MessageDeletedEvent(this.messageId);
}

/// The server marked the whole room read on our behalf (sent right after the
/// connect handshake).
class AllReadEvent extends SocketEvent {
  const AllReadEvent();
}

/// A server-side rejection, e.g. "Sharing contact info is prohibited." or
/// "Contract ended." Surfaced verbatim -- swallowing these is why blocked
/// messages used to vanish with no explanation.
class SocketErrorEvent extends SocketEvent {
  final String message;
  const SocketErrorEvent(this.message);
}

/// A notification row was pushed to us (see `NotificationService.PublishAsync`:
/// `{id, notification_type, target_role, message, is_read, created_at, related_id}`).
class NotificationPushEvent extends SocketEvent {
  final Map<String, dynamic> notification;
  const NotificationPushEvent(this.notification);
}

class ConnectionStateEvent extends SocketEvent {
  final bool connected;
  final String? path;
  const ConnectionStateEvent(this.connected, this.path);
}

/// Real-time transport for chat and notifications.
///
/// The server is a raw WebSocket bridge at `/ws`, not Pusher and not socket.io
/// (`WebSocketBridgeMiddleware`). The previous implementation used
/// `pusher_channels_flutter` + a `ChatCredentialService` that asked
/// `GET /api/chat/rooms/` for `pusher_app_id` -- a key that endpoint does not
/// return. `appKey` therefore always stayed empty, `PusherHelper` bailed at its
/// `if (credentials.appKey.isEmpty) return;` guard, and no socket was ever
/// opened. Nothing in the backend or the React client mentions Pusher at all.
///
/// ## Wire contract
///
/// Auth: `?token=<jwt>` (the bridge also accepts `?access_token=`).
///
/// | direction | payload |
/// |---|---|
/// | -> room   | `{"type":"chat_message","message":"<text>"}` |
/// | -> room   | `{"type":"typing","is_typing":true}` |
/// | -> room   | `{"type":"delete_message","message_id":91}` |
/// | <- room   | `{"type":"chat_message","message":{id,text,sender_id,sender_role,created_at}}` |
/// | <- room   | `{"type":"typing","sender_id":"…","is_typing":true}` |
/// | <- room   | `{"type":"read_receipt","message_id":91}` |
/// | <- room   | `{"type":"delete","message_id":91}` |
/// | <- room   | `{"type":"all_read_update"}` |
/// | <- either | `{"type":"error","message":"…"}` |
///
/// Note the server broadcasts to *other* sockets only (`GetOthers`), so a
/// sender never receives their own message back over the socket -- the REST
/// `POST .../messages` response is the sender's copy.
class WebSocketService {
  WebSocketChannel? _room;
  WebSocketChannel? _notifications;

  StreamSubscription? _roomSub;
  StreamSubscription? _notificationsSub;

  final _events = StreamController<SocketEvent>.broadcast();
  Stream<SocketEvent> get events => _events.stream;

  int? _roomId;
  bool _disposed = false;

  /// Guards the reconnect timers so a rapid open/close cycle (switching rooms)
  /// cannot leave two sockets racing for the same room.
  Timer? _roomRetry;
  Timer? _notificationRetry;
  int _roomAttempts = 0;
  int _notificationAttempts = 0;

  /// Room socket state, for the "reconnecting" affordance in the conversation
  /// header.
  bool get isRoomConnected => _room != null;

  String _url(String path) {
    // A JWT is base64url, so it can never contain a space and `+` is not a
    // concern here -- but the endpoint parses this as a query value, so it goes
    // through the same helper as every other request rather than relying on that.
    final token = encodeQueryValue(getToken);
    return '$webSocketEndPoint$path?token=$token';
  }

  // ── Chat room ───────────────────────────────────────────────────────────────

  Future<void> connectToRoom(int roomId) async {
    if (_disposed) return;
    if (_roomId == roomId && _room != null) return;

    await disconnectRoom();
    _roomId = roomId;
    _roomAttempts = 0;
    _openRoom();
  }

  void _openRoom() {
    final roomId = _roomId;
    if (roomId == null || _disposed) return;

    final channel = WebSocketChannel.connect(Uri.parse(_url('/ws/chat/$roomId')));
    _room = channel;

    _roomSub = channel.stream.listen(
      _handleFrame,
      onError: (Object e) => _scheduleRoomRetry('error: $e'),
      onDone: () => _scheduleRoomRetry('closed'),
      cancelOnError: false,
    );

    // `WebSocketChannel.connect` returns before the handshake finishes, so the
    // old code emitted ConnectionStateEvent(true) *synchronously* from here.
    // When the room was opened from inside `build` (ConversationView passes the
    // fetch as a CustomFutureWidget `function`), the listener's `notifyListeners`
    // ran mid-build: "setState() or markNeedsBuild() called during build." The
    // connected event now follows `channel.ready` (the completed handshake).
    //
    // `.timeout` is also the connect deadline. The observed device failure was
    // `SocketException: Connection timed out (errno 110)` — a connect that never
    // completed — and without a deadline that waits out the OS default (≈2 min on
    // Android) before the stream errors, so no retry ran and the UI reported
    // "connected" the whole time. A deadline breach tears the socket down and
    // schedules the capped backoff retry instead of leaving a zombie channel.
    channel.ready
        .timeout(const Duration(seconds: 10))
        .then((_) {
          if (_disposed || _roomId != roomId) return;
          _emit(ConnectionStateEvent(true, '/ws/chat/$roomId'));
        })
        .catchError((Object e) =>
            _scheduleRoomRetry('connect failed/timeout: $e'));
  }

  void _scheduleRoomRetry(String reason) {
    if (_disposed || _roomId == null) return;
    debugPrint('[ws] room ${_roomId!} dropped ($reason)');
    _roomSub?.cancel();
    _roomSub = null;
    _room = null;
    _roomRetry?.cancel();
    // Capped exponential backoff. The bridge is push-only, so a dropped socket
    // is invisible to the user until they try to send.
    final delay = Duration(seconds: (1 << _roomAttempts.clamp(0, 5)));
    _roomAttempts++;
    _roomRetry = Timer(delay, () {
      _roomAttempts = 0;
      _openRoom();
    });
  }

  Future<void> disconnectRoom() async {
    _roomRetry?.cancel();
    _roomRetry = null;
    _roomAttempts = 0;
    _roomId = null;
    await _roomSub?.cancel();
    _roomSub = null;
    final room = _room;
    _room = null;
    await room?.sink.close();
  }

  /// Returns false when there is no live socket, so the caller can fall back to
  /// the REST send rather than silently dropping the message.
  bool sendChatMessage(String text) => _send({'type': 'chat_message', 'message': text});

  bool sendTyping(bool isTyping) => _send({'type': 'typing', 'is_typing': isTyping});

  bool deleteMessage(int messageId) =>
      _send({'type': 'delete_message', 'message_id': messageId});

  bool _send(Map<String, dynamic> payload) {
    final room = _room;
    if (room == null) return false;
    try {
      room.sink.add(jsonEncode(payload));
      return true;
    } catch (e) {
      debugPrint('[ws] send failed: $e');
      return false;
    }
  }

  // ── Notifications ───────────────────────────────────────────────────────────

  /// One long-lived socket for the signed-in user. Opened after sign-in and
  /// kept for the life of the session.
  Future<void> connectNotifications() async {
    if (_disposed) return;
    if (_notifications != null) return;
    _notificationAttempts = 0;
    _openNotifications();
  }

  void _openNotifications() {
    if (_disposed || getToken.isEmpty) return;
    final channel = WebSocketChannel.connect(Uri.parse(_url('/ws/notifications')));
    _notifications = channel;

    _notificationsSub = channel.stream.listen(
      _handleFrame,
      onError: (Object e) => _scheduleNotificationRetry('error: $e'),
      onDone: () => _scheduleNotificationRetry('closed'),
      cancelOnError: false,
    );

    // Same handshake discipline as the room socket: only report "connected"
    // once the upgrade completed, and never leave a zombie channel behind.
    channel.ready
        .timeout(const Duration(seconds: 10))
        .then((_) {
          if (_disposed) return;
          _emit(ConnectionStateEvent(true, '/ws/notifications'));
        })
        .catchError((Object e) =>
            _scheduleNotificationRetry('connect failed/timeout: $e'));
  }

  void _scheduleNotificationRetry(String reason) {
    if (_disposed) return;
    debugPrint('[ws] notifications socket dropped ($reason)');
    _notificationsSub?.cancel();
    _notificationsSub = null;
    _notifications = null;
    _notificationRetry?.cancel();
    final delay = Duration(seconds: (1 << _notificationAttempts.clamp(0, 5)));
    _notificationAttempts++;
    _notificationRetry = Timer(delay, () {
      _notificationAttempts = 0;
      _openNotifications();
    });
  }

  Future<void> disconnectNotifications() async {
    _notificationRetry?.cancel();
    _notificationRetry = null;
    await _notificationsSub?.cancel();
    _notificationsSub = null;
    final socket = _notifications;
    _notifications = null;
    await socket?.sink.close();
  }

  // ── Frame dispatch ──────────────────────────────────────────────────────────

  void _handleFrame(dynamic raw) {
    if (raw is! String) return;
    Map<String, dynamic> frame;
    try {
      final decoded = jsonDecode(raw);
      if (decoded is! Map) return;
      frame = Map<String, dynamic>.from(decoded);
    } catch (e) {
      debugPrint('[ws] unparseable frame: $raw');
      return;
    }

    final type = frame['type']?.toString();
    switch (type) {
      case 'chat_message':
        final message = frame['message'];
        if (message is Map) {
          _emit(ChatMessageEvent(Map<String, dynamic>.from(message)));
        }
        break;
      case 'typing':
        _emit(TypingEvent(
          frame['sender_id']?.toString() ?? '',
          frame['is_typing'] == true,
        ));
        break;
      case 'read_receipt':
        final id = frame['message_id'];
        if (id is num) _emit(ReadReceiptEvent(id.toInt()));
        break;
      case 'delete':
        final id = frame['message_id'];
        if (id is num) _emit(MessageDeletedEvent(id.toInt()));
        break;
      case 'all_read_update':
        _emit(const AllReadEvent());
        break;
      case 'error':
        _emit(SocketErrorEvent(frame['message']?.toString() ?? 'Unknown error'));
        break;
      // A pushed notification row has no `type` envelope -- it *is* the row --
      // and is identified by `notification_type`.
      default:
        if (frame.containsKey('notification_type')) {
          _emit(NotificationPushEvent(frame));
        }
    }
  }

  void _emit(SocketEvent event) {
    if (_disposed) return;
    if (!_events.isClosed) _events.add(event);
  }

  Future<void> disposeAll() async {
    _disposed = true;
    await disconnectRoom();
    await disconnectNotifications();
    await _events.close();
  }
}
