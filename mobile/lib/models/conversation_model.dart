import 'dart:convert';

import '../helper/json_coercion.dart';

ConversationModel conversationModelFromJson(String str) =>
    ConversationModel.fromJson(json.decode(str));

String conversationModelToJson(ConversationModel data) => json.encode(data.toJson());

/// `GET /api/chat/rooms/{roomId}/messages?pageSize=&beforeMessageId=`
/// answers `{ data: MessageListResponse }`:
///
/// ```json
/// { "data": { "room_id": 3, "total_count": 42, "has_more": true,
///             "items": [ { "id": 91, "room_id": 3, "sender_id": "…",
///                          "sender_role": "freelancer", "sender_name": "…",
///                          "text": "hi", "is_read": true, "is_deleted": false,
///                          "created_at": "2026-09-28T10:00:00.000Z" } ] } }
/// ```
///
/// The previous model branched on `data.results`, `data` and camelCase
/// `senderId`/`roomId`/`isRead`/`createdAt`. None of those keys exist, so every
/// branch missed, `all_message` was null, and the thread rendered empty on a
/// 200.
///
/// Ordering: the API returns the **newest page first** (cursor walks backwards
/// via `before_message_id`), and this list is displayed oldest-at-top, so
/// [messages] is reversed on the way in.
class ConversationModel {
  AllMessage? allMessage;

  ConversationModel({this.allMessage});

  factory ConversationModel.fromJson(Map json) {
    // Accept both the real `{data: {...}}` envelope and a bare page object, so a
    // future `Ok(messageList)` does not silently blank the thread again.
    final payload = json['data'] is Map ? json['data'] as Map : json;
    return ConversationModel(
      allMessage: AllMessage.fromJson(Map<String, dynamic>.from(payload)),
    );
  }

  Map<String, dynamic> toJson() => {
        'data': allMessage?.toJson(),
      };
}

class AllMessage {
  List<Datum>? data;
  int totalCount;
  bool hasMore;

  AllMessage({this.data, this.totalCount = 0, this.hasMore = false});

  factory AllMessage.fromJson(Map<String, dynamic> json) {
    final raw = json['items'];
    final items = raw is List
        ? raw
            .whereType<Map>()
            .map((e) => Datum.fromJson(Map<String, dynamic>.from(e)))
            .toList()
        : <Datum>[];
    // Newest-first from the server, oldest-first on screen.
    return AllMessage(
      data: items.reversed.toList(),
      totalCount: asIntOrNull(json['total_count']) ?? items.length,
      hasMore: json['has_more'] == true,
    );
  }

  Map<String, dynamic> toJson() => {
        'total_count': totalCount,
        'has_more': hasMore,
        'items': data?.map((e) => e.toJson()).toList() ?? [],
      };
}

class Datum {
  int? id;
  int? roomId;

  /// The sender's **user** id (a Guid string), which is what
  /// `ProfileInfoService.data.id` holds -- that is how "is this my message?"
  /// is decided. The old `fromUser` was `"1"`/`"2"` role codes.
  String? senderId;
  String? senderRole;
  String? senderName;
  String? text;
  bool isRead;
  bool isDeleted;
  DateTime? createdAt;

  Datum({
    this.id,
    this.roomId,
    this.senderId,
    this.senderRole,
    this.senderName,
    this.text,
    this.isRead = false,
    this.isDeleted = false,
    this.createdAt,
  });

  factory Datum.fromJson(Map<String, dynamic> json) => Datum(
        id: json['id'] is num ? (json['id'] as num).toInt() : null,
        roomId: json['room_id'] is num ? (json['room_id'] as num).toInt() : null,
        senderId: json['sender_id']?.toString(),
        senderRole: json['sender_role']?.toString(),
        senderName: json['sender_name']?.toString(),
        text: json['text']?.toString(),
        // `bool IsRead` on the backend, so a real JSON bool.
        isRead: json['is_read'] == true,
        isDeleted: json['is_deleted'] == true,
        createdAt: json['created_at'] == null
            ? null
            : DateTime.tryParse(json['created_at'].toString()),
      );

  /// Payload shape the WebSocket broadcast uses for an incoming message
  /// (WebSocketBridgeMiddleware:74). It carries a subset of the REST fields --
  /// no `sender_name`, no `is_read` -- so those stay null/false.
  factory Datum.fromSocketMessage(Map<String, dynamic> json) => Datum(
        id: json['id'] is num ? (json['id'] as num).toInt() : null,
        senderId: json['sender_id']?.toString(),
        senderRole: json['sender_role']?.toString(),
        text: json['text']?.toString(),
        createdAt: json['created_at'] == null
            ? null
            : DateTime.tryParse(json['created_at'].toString()),
      );

  Map<String, dynamic> toJson() => {
        'id': id,
        'room_id': roomId,
        'sender_id': senderId,
        'sender_role': senderRole,
        'sender_name': senderName,
        'text': text,
        'is_read': isRead,
        'is_deleted': isDeleted,
        'created_at': createdAt?.toIso8601String(),
      };
}
