import 'package:webofwork/helper/json_coercion.dart';

/// One page of `GET /api/projects/my-proposals`
/// (`ProposalsController.GetMyProposals`).
///
/// ```json
/// { "count": 24, "next": "?page=2&pageSize=10", "previous": null,
///   "results": [ ProposalResponse, ... ] }
/// ```
///
/// `next` / `previous` are **relative** query strings, not absolute URLs.
class MyProposalsPage {
  final int count;
  final String? next;
  final String? previous;
  final List<MyProposal> results;

  const MyProposalsPage({
    this.count = 0,
    this.next,
    this.previous,
    this.results = const [],
  });

  static const empty = MyProposalsPage();

  bool get isEmpty => results.isEmpty;
  bool get hasNext => (next ?? '').isNotEmpty;

  /// Reads the paged envelope, a bare array, and the `data`-keyed shape the
  /// template expected, so the parser does not depend on which one arrives.
  factory MyProposalsPage.fromJson(dynamic payload) {
    if (payload is List) {
      return MyProposalsPage(
        count: payload.length,
        results: asObjectList(payload, MyProposal.fromJson),
      );
    }
    if (payload is! Map) return empty;
    final j = Map<String, dynamic>.from(payload);
    final rows = j['results'] ?? j['data'];
    return MyProposalsPage(
      count: asInt(j['count']),
      next: asOptString(j['next']),
      previous: asOptString(j['previous']),
      results: asObjectList(rows, MyProposal.fromJson),
    );
  }
}

/// `ProposalResponse` (`DTOs/Proposals/ProposalDtos.cs`).
///
/// Everything the previous `Proposal` class read came from the template: it looked
/// for `amount` and `duration` (the server sends `bid_amount` and
/// `delivery_days`), and for `is_hired` / `is_short_listed` /
/// `is_interview_take` / `is_view` / `is_rejected`, none of which exist on
/// `ProposalResponse` at all -- so every one of them rendered `false` and
/// `revision` / `attachment` rendered null. It also read a nested `job` object
/// for the project title, while the server flattens `project_title` onto the
/// row.
///
/// The template's wrapper is gone for the same reason it was broken in the
/// offers list: it looked for `json["my_proposals"]`, but this endpoint answers
/// `{count, next, previous, results}`. That key does not exist, so
/// `myProposals?.data` was always null and **the My Proposals list was
/// permanently empty** on top of the 404 from calling `/api/proposals/`, which is
/// not a route (the only GETs on that controller are `my` and
/// `project/{projectId}`).
class MyProposal {
  final int id;
  final int projectId;
  final String projectTitle;
  final String freelancerId;
  final String freelancerName;
  final String packageId;
  final String coverLetter;

  /// `decimal`, written by `DecimalAsStringJsonConverter`, so it arrives quoted.
  final double bidAmount;
  final int deliveryDays;
  final String status;
  final DateTime? createdAt;

  final String freelancerPhoto;
  final String freelancerTagline;
  final double freelancerRatings;

  /// Who the proposal is addressed to. `ProposalResponse.ClientName` -- added
  /// to the server because the mobile details sheet and the web proposal are
  /// both much more useful with a name than a raw `client_id`.
  final String clientName;

  /// The contract this proposal turned into, or null while it is still pending.
  final MyProposalContract? contractInfo;

  const MyProposal({
    required this.id,
    this.projectId = 0,
    this.projectTitle = '',
    this.freelancerId = '',
    this.freelancerName = '',
    this.packageId = '',
    this.coverLetter = '',
    this.bidAmount = 0,
    this.deliveryDays = 0,
    this.status = '',
    this.createdAt,
    this.freelancerPhoto = '',
    this.freelancerTagline = '',
    this.freelancerRatings = 0,
    this.clientName = '',
    this.contractInfo,
  });

  factory MyProposal.fromJson(Map json) {
    final j = Map<String, dynamic>.from(json);
    return MyProposal(
      id: asInt(j['id']),
      projectId: asInt(j['project_id']),
      projectTitle: (j['project_title'] ?? '').toString(),
      freelancerId: (j['freelancer_id'] ?? '').toString(),
      freelancerName: (j['freelancer_name'] ?? '').toString(),
      packageId: (j['package_id'] ?? '').toString(),
      coverLetter: (j['cover_letter'] ?? '').toString(),
      bidAmount: asDouble(j['bid_amount']),
      deliveryDays: asInt(j['delivery_days']),
      // `ProposalStatus` goes through `JsonStringEnumConverter(SnakeCaseLower)`.
      status: (j['status'] ?? '').toString(),
      createdAt: asDateOrNull(j['created_at']),
      freelancerPhoto: (j['freelancer_photo'] ?? '').toString(),
      freelancerTagline: (j['freelancer_tagline'] ?? '').toString(),
      freelancerRatings: asDouble(j['freelancer_ratings']),
      clientName: (j['client_name'] ?? '').toString(),
      contractInfo: j['contract_info'] is Map
          ? MyProposalContract.fromJson(
              Map<String, dynamic>.from(j['contract_info'] as Map))
          : null,
    );
  }

  // ── Status ────────────────────────────────────────────────────────────────
  //
  // `ProposalStatus` (Models/Enums/ProposalEnums.cs) is Pending=0, Accepted=1,
  // Rejected=2, Withdrawn=3 and is emitted by
  // `JsonStringEnumConverter(SnakeCaseLower)`, so these are the exact wire
  // values. Never compare against ordinals -- the template's
  // `isHired`/`isShortListed`/`isRejected` flags were template columns that this
  // enum simply does not have, which is why every proposal rendered as
  // "not rejected, not shortlisted, not interviewed" no matter its real state.

  static const statusPending = 'pending';
  static const statusAccepted = 'accepted';
  static const statusRejected = 'rejected';
  static const statusWithdrawn = 'withdrawn';

  bool get isPending => status == statusPending;
  bool get isAccepted => status == statusAccepted;
  bool get isRejected => status == statusRejected;
  bool get isWithdrawn => status == statusWithdrawn;

  /// True once a contract exists, i.e. the client acted on this proposal.
  bool get isActioned => contractInfo != null;

  /// `ProposalService.WithdrawProposalAsync` throws unless the row is
  /// `Pending`, and returns 400 with "Only pending proposals can be withdrawn."
  /// The button is hidden rather than shown-then-rejected.
  bool get canWithdraw => isPending;

  String get statusLabel => switch (status) {
        statusPending => 'Pending',
        statusAccepted => 'Accepted',
        statusRejected => 'Rejected',
        statusWithdrawn => 'Withdrawn',
        '' => 'Unknown',
        _ => status,
      };
}

class MyProposalContract {
  final int id;
  final String status;
  final bool isThisFreelancer;

  const MyProposalContract({
    required this.id,
    this.status = '',
    this.isThisFreelancer = false,
  });

  factory MyProposalContract.fromJson(Map<String, dynamic> json) => MyProposalContract(
        id: asInt(json['id']),
        // Already a snake_case string on the server, so this is verbatim.
        status: (json['status'] ?? '').toString(),
        isThisFreelancer: asBool(json['is_this_freelancer']),
      );
}
