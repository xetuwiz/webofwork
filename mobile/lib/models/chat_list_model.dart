import 'dart:convert';

ChatListModel chatListModelFromJson(String str) =>
    ChatListModel.fromJson(json.decode(str));

String chatListModelToJson(ChatListModel data) => json.encode(data.toJson());

/// `GET /api/chat/rooms` answers `{ "data": [ ChatRoomResponse, ... ] }` -- a
/// **bare array** under `data`, with no paging envelope.
///
/// The previous model probed for `unreadCount` / `lastMessageAt` /
/// `createdAt` (camelCase) before falling back to the template's
/// `client_unseen_msg_count` / `updated_at` keys. The API emits snake_case, so
/// every room parsed as an all-null object: blank name, no preview, and a
/// permanent zero unread badge.
///
/// ```json
/// { "id": 3, "contract_id": 12,
///   "client":     { "id": "…", "name": "Acme Ltd",  "role": "Client" },
///   "freelancer": { "id": "…", "name": "Xetuwiz",   "role": "Freelancer" },
///   "last_message_preview": "…", "last_message_at": "…",
///   "unread_count": 2, "is_archived": false,
///   "contract_status": "active", "created_at": "…" }
/// ```
///
/// Note `ChatParticipantDto` has **no** photo field, so the avatar falls back to
/// the deterministic colour + initials treatment in `ChatTileAvatar`.
class ChatListModel {
  ChatList? chatList;

  ChatListModel({this.chatList});

  /// The endpoint answers `{data: [room, ...]}` — one bare array, no envelope.
  ///
  /// ## The dead branch that used to be here
  ///
  /// This factory had a second path for payloads shaped like the template's
  /// Laravel envelope, reading `chat_list`, `active_users` and `activity_check`.
  /// **None of those keys exist on this API**, and the branch was reachable
  /// whenever `data` was not a `List` — so any shape change produced a
  /// structurally-valid `ChatList` parsed from `null` rows rather than an honest
  /// empty inbox. That is the same "returns 200, renders nothing" failure class
  /// as the `job_details` and `my_offers` wrappers.
  ///
  /// `active_users` and `activity_check` are gone entirely: they drove the green
  /// presence dot on the chat tile, which `ChatParticipantDto` cannot support
  /// (it has no photo or presence field at all). The dot was already removed for
  /// that reason; the fields were left behind.
  factory ChatListModel.fromJson(Map json) {
    final rooms = json['data'];
    return ChatListModel(
      chatList: rooms == null
          ? ChatList(chats: const [])
          : ChatList.fromJson(<String, dynamic>{'data': rooms}),
    );
  }

  Map<String, dynamic> toJson() => {
        'data': chatList?.toJson(),
      };
}

class ChatList {
  List<Chats>? chats;

  ChatList({this.chats});

  /// `GET /api/chat/rooms` has no paging envelope -- one bare `data` array with
  /// every room -- so there is no `next_page_url` to follow. The field the old
  /// model read here never appeared in a response, which is why the inbox's
  /// infinite scroll never loaded a second page.
  factory ChatList.fromJson(Map<String, dynamic> json) => ChatList(
        chats: json['data'] == null
            ? []
            : List<Chats>.from((json['data'] as List)
                .whereType<Map>()
                .map((x) => Chats.fromJson(Map<String, dynamic>.from(x)))),
      );

  Map<String, dynamic> toJson() => {
        'data': chats?.map((e) => e.toJson()).toList() ?? [],
      };
}

class Chats {
  int? id;
  int? contractId;
  DateTime? createdAt;

  /// `last_message_at` -- the only meaningful timestamp on the row, and what the
  /// list sorts by.
  DateTime? lastMessageAt;
  String? lastMessagePreview;
  int unreadCount;
  bool isArchived;
  String? contractStatus;
  Client? client;
  Client? freelancer;

  Chats({
    this.id,
    this.contractId,
    this.createdAt,
    this.lastMessageAt,
    this.lastMessagePreview,
    this.unreadCount = 0,
    this.isArchived = false,
    this.contractStatus,
    this.client,
    this.freelancer,
  });

  factory Chats.fromJson(Map<String, dynamic> json) => Chats(
        id: json['id'] is num ? (json['id'] as num).toInt() : null,
        contractId:
            json['contract_id'] is num ? (json['contract_id'] as num).toInt() : null,
        createdAt: _date(json['created_at']),
        lastMessageAt: _date(json['last_message_at']),
        lastMessagePreview: json['last_message_preview']?.toString(),
        unreadCount: json['unread_count'] is num
            ? (json['unread_count'] as num).toInt()
            : int.tryParse(json['unread_count']?.toString() ?? '') ?? 0,
        isArchived: json['is_archived'] == true,
        contractStatus: json['contract_status']?.toString(),
        client: json['client'] is Map ? Client.fromJson(json['client']) : null,
        freelancer:
            json['freelancer'] is Map ? Client.fromJson(json['freelancer']) : null,
      );

  /// The participant on the other side, from the signed-in user's point of
  /// view. The role of each participant is in the payload (`role`), so this
  /// picks the one that is not the current user when it can, and otherwise
  /// falls back to the client.
  Client? counterpartFor(String? selfUserId) {
    if (selfUserId == null || selfUserId.isEmpty) return client ?? freelancer;
    if (freelancer?.id.toString() == selfUserId) return client;
    if (client?.id.toString() == selfUserId) return freelancer;
    return client ?? freelancer;
  }

  static DateTime? _date(dynamic v) => v == null ? null : DateTime.tryParse(v.toString());

  Map<String, dynamic> toJson() => {
        'id': id,
        'contract_id': contractId,
        'created_at': createdAt?.toIso8601String(),
        'last_message_at': lastMessageAt?.toIso8601String(),
        'last_message_preview': lastMessagePreview,
        'unread_count': unreadCount,
        'is_archived': isArchived,
        'contract_status': contractStatus,
        'client': client?.toJson(),
        'freelancer': freelancer?.toJson(),
      };
}

class Client {
  String? id;
  String? name;
  String? role;

  /// `ChatParticipantDto` has no avatar field. Kept so a future DTO addition
  /// (or a joined profile lookup) can populate it without touching call sites;
  /// null makes `ChatTileAvatar` fall back to initials on a colour derived from
  /// the room id.
  String? image;

  Client({this.id, this.name, this.role, this.image});

  factory Client.fromJson(Map<String, dynamic> json) => Client(
        id: json['id']?.toString(),
        name: json['name']?.toString(),
        role: json['role']?.toString(),
        image: json['profile_photo']?.toString(),
      );

  Map<String, dynamic> toJson() => {
        'id': id,
        'name': name,
        'role': role,
        'profile_photo': image,
      };
}
