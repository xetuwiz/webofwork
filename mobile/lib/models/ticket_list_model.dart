import 'dart:convert';

import '../helper/json_coercion.dart';

TicketListModel ticketListModelFromJson(String str) =>
    TicketListModel.fromJson(json.decode(str));

/// Support tickets. `GET /api/support/tickets?page=&pageSize=` answers a
/// **bare** `SupportTicketListResponse` (not wrapped in `data`):
///
/// ```json
/// { "items": [ { "id": 4, "user_id": "…", "user_email": "…",
///                 "sender_role": "freelancer",
///                 "subject": "…", "message": "…",
///                 "status": "open", "admin_reply": null,
///                 "resolved_at": null, "resolved_by_admin_id": null,
///                 "created_at": "…", "updated_at": "…" } ],
///   "total_count": 9, "open_count": 2, "page": 1, "page_size": 20,
///   "total_pages": 1 }
/// ```
///
/// The previous model read `json["tickets"]["data"]` and `next_page_url`. The
/// API has no `tickets` envelope and no `next_page_url`, so `json["tickets"]`
/// was `null`, the `!` on it threw, and the list screen never rendered.
///
/// There is deliberately **no** per-ticket message thread: the support model is
/// "one question, at most one admin reply". The template's
/// `TicketChatService` / `TicketChatModel` / `ticket_chat_view` trio (and its
/// nonexistent `/messages` routes) were removed for that reason; the admin
/// reply is shown inline on the ticket instead.
///
/// `SupportTicketStatus` serialises as `open` / `resolved` / `cancelled`
/// (`JsonStringEnumConverter(SnakeCaseLower)`), not the `low/medium/high`
/// priority strings the old tile colour-coded.
class TicketListModel {
  List<Ticket> data;
  int totalCount;
  int openCount;
  int page;
  int pageSize;
  int totalPages;

  /// Paging is page-number based, not cursor based, so there is no `next`
  /// URL to follow.
  dynamic nextPageUrl;

  TicketListModel({
    List<Ticket>? data,
    this.totalCount = 0,
    this.openCount = 0,
    this.page = 1,
    this.pageSize = 20,
    this.totalPages = 1,
    this.nextPageUrl,
  }) : data = data ?? const [];

  bool get hasMore => page < totalPages;

  factory TicketListModel.fromJson(Map json) {
    final raw = json['items'];
    return TicketListModel(
      data: raw is List
          ? raw
              .whereType<Map>()
              .map((e) => Ticket.fromJson(Map<String, dynamic>.from(e)))
              .toList()
          : const <Ticket>[],
      totalCount: asInt(json['total_count']),
      openCount: asInt(json['open_count']),
      // Absent or unparseable `page` means the server has no page concept for
      // this query, so page 1 is the honest answer rather than 0.
      page: asIntOrNull(json['page']) ?? 1,
      pageSize: asInt(json['page_size']),
      totalPages: asInt(json['total_pages']),
    );
  }

  Map<String, dynamic> toJson() => {
        'items': data.map((e) => e.toJson()).toList(),
        'total_count': totalCount,
        'open_count': openCount,
        'page': page,
        'page_size': pageSize,
        'total_pages': totalPages,
      };
}

class Ticket {
  int id;
  String? userId;
  String? userEmail;

  /// `sender_role` -- `freelancer` for a ticket raised in this app.
  String? senderRole;
  String subject;
  String message;

  /// `open` | `resolved` | `cancelled`.
  String status;

  /// The single admin response. Non-null means an admin has answered; this is
  /// the only "conversation" the support domain has.
  String? adminReply;
  DateTime? resolvedAt;
  String? resolvedByAdminId;
  DateTime? createdAt;
  DateTime? updatedAt;

  Ticket({
    required this.id,
    this.userId,
    this.userEmail,
    this.senderRole,
    this.subject = '',
    this.message = '',
    this.status = 'open',
    this.adminReply,
    this.resolvedAt,
    this.resolvedByAdminId,
    this.createdAt,
    this.updatedAt,
  });

  bool get isOpen => status == 'open';
  bool get isCancelled => status == 'cancelled';
  bool get hasReply => (adminReply ?? '').trim().isNotEmpty;

  factory Ticket.fromJson(Map<String, dynamic> json) => Ticket(
        id: asInt(json['id']),
        userId: json['user_id']?.toString(),
        userEmail: json['user_email']?.toString(),
        senderRole: json['sender_role']?.toString(),
        subject: json['subject']?.toString() ?? '',
        // The request field is `message`, not the template's `description`.
        message: json['message']?.toString() ?? '',
        status: json['status']?.toString() ?? 'open',
        adminReply: json['admin_reply']?.toString(),
        resolvedAt: json['resolved_at'] == null
            ? null
            : DateTime.tryParse(json['resolved_at'].toString()),
        resolvedByAdminId: json['resolved_by_admin_id']?.toString(),
        createdAt: json['created_at'] == null
            ? null
            : DateTime.tryParse(json['created_at'].toString()),
        updatedAt: json['updated_at'] == null
            ? null
            : DateTime.tryParse(json['updated_at'].toString()),
      );

  Map<String, dynamic> toJson() => {
        'id': id,
        'user_id': userId,
        'user_email': userEmail,
        'sender_role': senderRole,
        'subject': subject,
        'message': message,
        'status': status,
        'admin_reply': adminReply,
        'resolved_at': resolvedAt?.toIso8601String(),
        'resolved_by_admin_id': resolvedByAdminId,
        'created_at': createdAt?.toIso8601String(),
        'updated_at': updatedAt?.toIso8601String(),
      };
}
