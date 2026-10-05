import 'dart:convert';

import '../helper/json_coercion.dart';

NotificationListModel notificationListModelFromJson(String str) =>
    NotificationListModel.fromJson(json.decode(str));

String notificationListModelToJson(NotificationListModel data) =>
    json.encode(data.toJson());

/// `GET /api/notifications/` answers
/// `{"data": {"unread_count": n, "total_count": n, "items": [...]}}`.
///
/// The endpoint takes `?isRead=` and `?limit=` but has **no page/`next`** — the
/// whole list comes back in one response, so `nextPageUrl` is always null and
/// `unreadCount` is the only source of truth for the badge. There is no
/// unread-count route on this API.
class NotificationListModel {
  List<AppNotification>? notifications;
  int unreadCount;
  int totalCount;

  NotificationListModel({
    this.notifications,
    this.unreadCount = 0,
    this.totalCount = 0,
  });

  factory NotificationListModel.fromJson(Map json) {
    // The envelope key is `data`; the previous model read `data` as a *list*, so
    // the list rendered empty even on a 200.
    final data = json["data"];
    final container = data is Map ? data : const <String, dynamic>{};
    final items = container["items"] ?? data; // tolerate a bare array too
    return NotificationListModel(
      notifications: items is List
          ? items
              .whereType<Map>()
              .map((e) => AppNotification.fromJson(Map<String, dynamic>.from(e)))
              .toList()
          : <AppNotification>[],
      unreadCount: asInt(container["unread_count"]),
      totalCount: asInt(container["total_count"]),
    );
  }

  Map<String, dynamic> toJson() => {
        "data": {
          "unread_count": unreadCount,
          "total_count": totalCount,
          "items": notifications == null
              ? []
              : List<dynamic>.from(notifications!.map((x) => x.toJson())),
        },
      };
}

/// One row from `GET /api/notifications`.
///
/// Named `AppNotification`, not `Notification`, because Flutter's
/// `material.dart` already exports a `Notification` widget and every consumer of
/// this model needs `material.dart`. The bare name forced a `hide Notification`
/// import in each of them; renaming the model once removes the collision at the
/// source.
class AppNotification {
  int id;

  /// One of the 19 snake_case values in
  /// `server/WebOfWork.Api/Models/NotificationWireFormat.cs`, e.g.
  /// `proposal_accepted`, `delivered`, `new_message`. Kept as a raw string
  /// because the enum is open-ended — the old `typeValues.map[json["type"]]!`
  /// used a null assertion and threw on any value the template did not know,
  /// which took down the whole list.
  final String type;
  String message;

  /// `bool IsRead` on the backend, so this arrives as `true`/`false`. The
  /// template expected the strings `"read"`/`"unread"`, which never matched.
  bool isRead;
  DateTime? createdAt;

  /// `related_entity_type` / `related_entity_id` -- what the row points at
  /// (e.g. `contract`, `proposal`, `message`). The template had no fields for
  /// these, so tapping a notification could not navigate anywhere.
  final String? relatedEntityType;
  final int? relatedEntityId;
  final String? targetRole;

  AppNotification({
    required this.id,
    required this.type,
    required this.message,
    required this.isRead,
    this.createdAt,
    this.relatedEntityType,
    this.relatedEntityId,
    this.targetRole,
  });

  factory AppNotification.fromJson(Map<String, dynamic> json) => AppNotification(
        id: asInt(json["id"]),
        // `type` is what both `GET /api/notifications` and the WebSocket push
        // send. `notification_type` was the push field name before the server was
        // changed to project `NotificationResponse` for the socket too; it is
        // still accepted so a client that reconnects to a not-yet-restarted
        // backend does not render every push as "unknown".
        type: json["type"]?.toString() ??
            json["notification_type"]?.toString() ??
            "unknown",
        message: json["message"]?.toString() ?? "",
        isRead: _parseIsRead(json["is_read"]),
        createdAt: json["created_at"] == null
            ? null
            : DateTime.tryParse(json["created_at"].toString()),
        relatedEntityType: json["related_entity_type"]?.toString(),
        // `related_entity_id` in the list response; `related_id` in the old push
        // shape, which carried no `related_entity_type` at all -- so a push told
        // the client *that* something happened but never *what*, and tapping it
        // could not navigate anywhere. Reading both means an older backend's push
        // still deep-links.
        //
        // Coerced with `asIntOrNull`, not `asInt`: this id is genuinely optional
        // (`RelatedEntityId` is a nullable column) and the tile's tappability is
        // `relatedEntityId != null`. Coercing to 0 made every target-less
        // notification look tappable and pushed `MyContractDetailsView` with id 0.
        relatedEntityId: asIntOrNull(
          json["related_entity_id"] ?? json["related_id"],
        ),
        targetRole: json["target_role"]?.toString(),
      );

  /// Accepts the bool the API sends, plus the legacy `"read"`/`"unread"`
  /// strings and 0/1, so an older cached payload still renders.
  static bool _parseIsRead(dynamic v) {
    if (v is bool) return v;
    if (v is num) return v != 0;
    switch (v?.toString().toLowerCase()) {
      case 'read':
      case 'true':
      case '1':
        return true;
      default:
        return false;
    }
  }

  Map<String, dynamic> toJson() => {
        "id": id,
        "type": type,
        "message": message,
        "is_read": isRead,
        "created_at": createdAt?.toIso8601String(),
        "related_entity_type": relatedEntityType,
        "related_entity_id": relatedEntityId,
        "target_role": targetRole,
      };
}
