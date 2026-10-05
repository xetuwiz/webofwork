import 'package:webofwork/helper/json_coercion.dart';

/// The coercion helpers (`asDouble`, `asInt`, `asStringList`, `asDateOrNull`,
/// `asObjectList`, `asOptString`, `asBool`) live in
/// `lib/helper/json_coercion.dart` and are re-exported here, because every model
/// in this app reads the same handful of badly-typed JSON fields and those
/// conversions must not drift apart between call sites.
export 'package:webofwork/helper/json_coercion.dart';

/// A **contract**, as the server actually returns it.
///
/// This file replaces two divergent copies of a template `Order` class
/// (`my_orders_model.dart` and `order_details_model.dart`, the second of which
/// shadowed the first via an import). Neither parsed anything real:
///
///  * `Order.fromJson` read a Laravel-shaped payload — `price`, `is_project_job`,
///    `commission_amount`, `payable_amount`, `job`, `user.first_name`,
///    `revision_left`, `total_hour` — **none of which exist** on
///    `ContractResponse`. Every field came out `null`, so the orders list and
///    the order details screen rendered blanks.
///  * `OrderDetailsModel.fromJson` branched on
///    `json.containsKey("projectTitle") || json.containsKey("totalAmount")`, but
///    the server emits **snake_case** (`project_title`, `total_amount`), so that
///    branch never fired and the detail screen fell through to
///    `json["order_details"]` — also absent. The whole screen was empty.
///  * The branch it was trying to reach mapped `status` from **integers**
///    (`1` = active, `3` = completed). `ContractStatus` is serialised by
///    `JsonStringEnumConverter(SnakeCaseLower)`, so the wire value is the string
///    `"active"` / `"completed"`. An `int` comparison against a string is always
///    false, which is why the dashboard's active-contracts list was permanently
///    empty.
///
/// There is no milestone or work-submission concept on this API. Work is
/// delivered via `Deliverable`s and changes are requested via `Revision`s, which
/// is what this model exposes.
class Contract {
  final int id;
  final int projectId;
  final String projectTitle;
  final String projectDescription;

  final String clientId;
  final String clientName;
  final String freelancerId;
  final String freelancerName;

  /// The freelancer's **user** id, distinct from [freelancerId] (the
  /// `freelancer_profiles` row). The chat endpoint matches on `users.id`, so
  /// passing [freelancerId] there fails its `EXISTS` lookup and 403s.
  final String? freelancerUserId;
  final String? clientUserId;

  final String? packageId;
  final String packageName;
  final int deliveryDays;

  /// A `decimal` written by `DecimalAsStringJsonConverter`, so it arrives as a
  /// **quoted string**. [asDouble] accepts either form.
  final double totalAmount;
  final double freelancerEarnings;

  final bool isFunded;
  final bool paidToFreelancer;
  final DateTime? payoutEligibleAfter;

  final bool clientHasSigned;
  final DateTime? clientSignedAt;
  final bool freelancerHasSigned;
  final DateTime? freelancerSignedAt;
  final String? legalDocumentUrl;

  /// When the contract row was created. The server used to omit this entirely,
  /// which left the orders screen with no "started" date at all; it is now
  /// backed by `Contract.CreatedAt` (`_created_at` column, `now()` default).
  final DateTime? createdAt;

  final DateTime? completedAt;
  final String status;
  final String? paymentStatus;

  final String? requirements;
  final String? expectedDeliverables;
  final List<String> skills;

  final List<ContractDeliverable> deliverables;
  final List<ContractRevision> revisions;

  /// The two reviews a completed contract can carry, embedded by the server as
  /// `reviews: { client_review: {...} | null, freelancer_review: {...} | null }`.
  /// Null until the corresponding party has written one.
  final ContractReview? clientReview;
  final ContractReview? freelancerReview;

  /// `dispute_details` on the contract response — null until someone raises one.
  final ContractDispute? dispute;

  const Contract({
    required this.id,
    required this.projectId,
    required this.projectTitle,
    required this.projectDescription,
    required this.clientId,
    required this.clientName,
    required this.freelancerId,
    required this.freelancerName,
    this.freelancerUserId,
    this.clientUserId,
    this.packageId,
    required this.packageName,
    required this.deliveryDays,
    required this.totalAmount,
    required this.freelancerEarnings,
    required this.isFunded,
    required this.paidToFreelancer,
    this.payoutEligibleAfter,
    required this.clientHasSigned,
    this.clientSignedAt,
    required this.freelancerHasSigned,
    this.freelancerSignedAt,
    this.legalDocumentUrl,
    this.createdAt,
    this.completedAt,
    required this.status,
    this.paymentStatus,
    this.requirements,
    this.expectedDeliverables,
    this.skills = const [],
    this.deliverables = const [],
    this.revisions = const [],
    this.clientReview,
    this.freelancerReview,
    this.dispute,
  });

  factory Contract.fromJson(Map json) {
    final j = Map<String, dynamic>.from(json);
    return Contract(
      id: asInt(j['id']),
      projectId: asInt(j['project_id']),
      projectTitle: (j['project_title'] ?? '').toString(),
      projectDescription: (j['project_description'] ?? '').toString(),
      clientId: (j['client_id'] ?? '').toString(),
      clientName: (j['client_name'] ?? '').toString(),
      freelancerId: (j['freelancer_id'] ?? '').toString(),
      freelancerName: (j['freelancer_name'] ?? '').toString(),
      freelancerUserId: asOptString(j['freelancer_user_id']),
      clientUserId: asOptString(j['client_user_id']),
      packageId: asOptString(j['package_id']),
      packageName: (j['package_name'] ?? '').toString(),
      deliveryDays: asInt(j['delivery_days']),
      totalAmount: asDouble(j['total_amount']),
      freelancerEarnings: asDouble(j['freelancer_earnings']),
      // `asBool`, not `== true`. These flags are `bool` columns today, but a
      // bare `== true` comparison silently drops any that arrive as `"true"` or
      // `1` -- and "is this contract funded?" is not a question to answer wrong
      // in the negative.
      isFunded: asBool(j['is_funded']),
      paidToFreelancer: asBool(j['paid_to_freelancer']),
      payoutEligibleAfter: asDateOrNull(j['payout_eligible_after']),
      clientHasSigned: asBool(j['client_has_signed']),
      clientSignedAt: asDateOrNull(j['client_signed_at']),
      freelancerHasSigned: asBool(j['freelancer_has_signed']),
      freelancerSignedAt: asDateOrNull(j['freelancer_signed_at']),
      legalDocumentUrl: asOptString(j['legal_document_url']),
      createdAt: asDateOrNull(j['created_at']),
      completedAt: asDateOrNull(j['completed_at']),
      status: (j['status'] ?? '').toString(),
      paymentStatus: asOptString(j['payment_status']),
      requirements: asOptString(j['requirements']),
      expectedDeliverables: asOptString(j['expected_deliverables']),
      // `RequiredTechnologies` is kept as an alias on the server, so both keys
      // can arrive; either is fine.
      skills: asStringList(j['skills'] ?? j['required_technologies']),
      deliverables: asObjectList(j['deliverables'], ContractDeliverable.fromJson),
      revisions: asObjectList(j['revisions'], ContractRevision.fromJson),
      clientReview: reviewIn(j['reviews'], 'client_review'),
      freelancerReview: reviewIn(j['reviews'], 'freelancer_review'),
      dispute: readDispute(j['dispute_details']),
    );
  }

  // ── Status ──────────────────────────────────────────────────────────────────
  //
  // `ContractStatus` is emitted by `JsonStringEnumConverter(SnakeCaseLower)`,
  // so these are the exact wire values. Never compare against ordinals.

  static const statusOffered = 'offered';
  static const statusActive = 'active';
  static const statusSubmitted = 'submitted';
  static const statusCompleted = 'completed';
  static const statusCancelled = 'cancelled';
  static const statusDisputed = 'disputed';
  static const statusRefunded = 'refunded';

  /// Worth surfacing on the dashboard: signed by both parties and running.
  /// "Live" means the freelancer has work in flight: `active`, `submitted`,
  /// or a contract under dispute (the money is still held and the work is
  /// unresolved, so it belongs with the freelancer's active pile, not the
  /// archive). A contract that is merely `offered` is not active work, so it
  /// is left for the offers screen.
  bool get isLive =>
      status == statusActive ||
      status == statusSubmitted ||
      status == statusDisputed;

  bool get isClosed =>
      status == statusCompleted || status == statusCancelled || status == statusRefunded;

  /// A contract the freelancer still has to sign. `POST /api/contracts/{id}/accept`
  /// is the route; the old `/{id}/sign/` path is not registered.
  bool get needsMySignature => !freelancerHasSigned && !isClosed;

  bool get isSignedByBoth => clientHasSigned && freelancerHasSigned;

  String get statusLabel => switch (status) {
        statusOffered => 'Offer',
        statusActive => 'Active',
        statusSubmitted => 'Submitted',
        statusCompleted => 'Completed',
        statusCancelled => 'Cancelled',
        statusDisputed => 'Disputed',
        statusRefunded => 'Refunded',
        '' => 'Unknown',
        _ => status,
      };

  /// Signed by both parties, so deliverables can be submitted against it.
  bool get acceptsWork => isSignedByBoth && !isClosed;

  /// A submitted deliverable can be corrected or removed only while the
  /// contract is running — `active` or `submitted`. Deliberately *not*
  /// `disputed`/`offered`: the server's `EnsureMutable` gate refuses edits once
  /// a contract is disputed, and the reference hides the controls before the
  /// server does.
  bool get canManageDeliverables =>
      status == statusActive || status == statusSubmitted;

  /// Reviews are only possible on a settled contract; the server enforces the
  /// same gate (`Completed` or `Refunded`) or it throws.
  bool get canSubmitReview => status == statusCompleted || status == statusRefunded;

  /// Used to fold a detail-screen action (accept, cancel, submit) back into a
  /// cached list without a full round trip.
  Contract copyWith({
    String? status,
    bool? freelancerHasSigned,
    DateTime? freelancerSignedAt,
    bool? clientHasSigned,
    DateTime? clientSignedAt,
    List<ContractDeliverable>? deliverables,
    List<ContractRevision>? revisions,
    ContractReview? clientReview,
    ContractReview? freelancerReview,
    ContractDispute? dispute,
  }) =>
      Contract(
        id: id,
        projectId: projectId,
        projectTitle: projectTitle,
        projectDescription: projectDescription,
        clientId: clientId,
        clientName: clientName,
        freelancerId: freelancerId,
        freelancerName: freelancerName,
        freelancerUserId: freelancerUserId,
        clientUserId: clientUserId,
        packageId: packageId,
        packageName: packageName,
        deliveryDays: deliveryDays,
        totalAmount: totalAmount,
        freelancerEarnings: freelancerEarnings,
        isFunded: isFunded,
        paidToFreelancer: paidToFreelancer,
        payoutEligibleAfter: payoutEligibleAfter,
        clientHasSigned: clientHasSigned ?? this.clientHasSigned,
        clientSignedAt: clientSignedAt ?? this.clientSignedAt,
        freelancerHasSigned: freelancerHasSigned ?? this.freelancerHasSigned,
        freelancerSignedAt: freelancerSignedAt ?? this.freelancerSignedAt,
        legalDocumentUrl: legalDocumentUrl,
        createdAt: createdAt,
        completedAt: completedAt,
        status: status ?? this.status,
        paymentStatus: paymentStatus,
        requirements: requirements,
        expectedDeliverables: expectedDeliverables,
        skills: skills,
        deliverables: deliverables ?? this.deliverables,
        revisions: revisions ?? this.revisions,
        clientReview: clientReview ?? this.clientReview,
        freelancerReview: freelancerReview ?? this.freelancerReview,
        dispute: dispute ?? this.dispute,
      );
}

/// A piece of submitted work. The unit that replaces the template's milestones.
class ContractDeliverable {
  final int id;
  final int contractId;
  final String title;
  final String? fileUrl;
  final String? linkUrl;
  final String? notes;
  final String deliverableType;
  final DateTime? submittedAt;

  const ContractDeliverable({
    required this.id,
    required this.contractId,
    required this.title,
    this.fileUrl,
    this.linkUrl,
    this.notes,
    required this.deliverableType,
    this.submittedAt,
  });

  factory ContractDeliverable.fromJson(Map json) {
    final j = Map<String, dynamic>.from(json);
    return ContractDeliverable(
      id: asInt(j['id']),
      contractId: asInt(j['contract_id']),
      title: (j['title'] ?? '').toString(),
      fileUrl: asOptString(j['file_url']),
      linkUrl: asOptString(j['link_url']),
      notes: asOptString(j['notes']),
      deliverableType: (j['deliverable_type'] ?? 'file').toString(),
      submittedAt: asDateOrNull(j['submitted_at']),
    );
  }

  /// An uploaded file takes precedence; a link is the fallback.
  String? get attachment => fileUrl ?? linkUrl;
  bool get isLink => (fileUrl ?? '').isEmpty && (linkUrl ?? '').isNotEmpty;
}

/// A requested change to submitted work.
///
/// `RevisionStatus` is `pending` / `accepted` / `rejected`.
class ContractRevision {
  final int id;
  final int contractId;
  final String userId;
  final String reason;
  final String status;
  final String? rejectionMessage;
  final DateTime? requestedAt;
  final DateTime? resolvedAt;

  const ContractRevision({
    required this.id,
    required this.contractId,
    required this.userId,
    required this.reason,
    required this.status,
    this.rejectionMessage,
    this.requestedAt,
    this.resolvedAt,
  });

  factory ContractRevision.fromJson(Map json) {
    final j = Map<String, dynamic>.from(json);
    return ContractRevision(
      id: asInt(j['id']),
      contractId: asInt(j['contract_id']),
      userId: (j['user_id'] ?? '').toString(),
      reason: (j['reason'] ?? '').toString(),
      status: (j['status'] ?? '').toString(),
      rejectionMessage: asOptString(j['rejection_message']),
      requestedAt: asDateOrNull(j['requested_at']),
      resolvedAt: asDateOrNull(j['resolved_at']),
    );
  }

  static const pending = 'pending';
  static const accepted = 'accepted';
  static const rejected = 'rejected';

  bool get isPending => status == pending;
}

/// One half of a contract's `reviews` block.
///
/// `review_type` distinguishes the two: `Client` (the freelancer reviewed the
/// client) vs `Freelancer` (the client reviewed the freelancer). The server
/// serialises `type` with the snake_case enum converter, so it arrives as
/// `"client"` / `"freelancer"`.
class ContractReview {
  final int id;
  final String type;
  final int rating;
  final String comment;
  final String? reviewerName;
  final DateTime? createdAt;

  const ContractReview({
    required this.id,
    required this.type,
    required this.rating,
    required this.comment,
    this.reviewerName,
    this.createdAt,
  });

  factory ContractReview.fromJson(Map json) {
    final j = Map<String, dynamic>.from(json);
    return ContractReview(
      id: asInt(j['id']),
      type: (j['type'] ?? '').toString(),
      rating: asInt(j['rating']),
      comment: (j['comment'] ?? '').toString(),
      reviewerName: asOptString(j['reviewer_name']),
      createdAt: asDateOrNull(j['created_at']),
    );
  }
}

/// `dispute_details` embedded in a contract response.
///
/// `reason` carries the reference wire label (`scope_creep`, `no_feedback`,
/// the literal `Unlimited Revisions`, …) and `status` is the snake_case
/// `pending` / `under_review` / `resolved` / `rejected`.
class ContractDispute {
  final int id;
  final int contractId;
  final String raisedById;
  final String? openerRole;
  final String reason;
  final String description;
  final String status;
  final String? adminNotes;
  final DateTime? createdAt;
  final DateTime? resolvedAt;
  final List<String> evidenceUrls;

  const ContractDispute({
    required this.id,
    required this.contractId,
    required this.raisedById,
    this.openerRole,
    required this.reason,
    required this.description,
    required this.status,
    this.adminNotes,
    this.createdAt,
    this.resolvedAt,
    this.evidenceUrls = const [],
  });

  factory ContractDispute.fromJson(Map json) {
    final j = Map<String, dynamic>.from(json);
    return ContractDispute(
      id: asInt(j['id']),
      contractId: asInt(j['contract_id']),
      raisedById: (j['raised_by_id'] ?? j['opener_id'] ?? '').toString(),
      openerRole: asOptString(j['opener_role']),
      reason: (j['reason'] ?? '').toString(),
      description: (j['description'] ?? '').toString(),
      status: (j['status'] ?? '').toString(),
      adminNotes: asOptString(j['admin_notes']),
      createdAt: asDateOrNull(j['created_at']),
      resolvedAt: asDateOrNull(j['resolved_at']),
      evidenceUrls: asStringList(j['evidence_urls']),
    );
  }

  static const statusPending = 'pending';
  static const statusUnderReview = 'under_review';
  static const statusResolved = 'resolved';
  static const statusRejected = 'rejected';

  bool get isOpen => status == statusPending || status == statusUnderReview;

  /// The app is freelancer-only, so a dispute opened by the freelancer is
  /// always "yours" even when the id comparison would need the user id the
  /// response does not carry on every embedding.
  bool get raisedByMe => openerRole == 'Freelancer';

  String get statusLabel => switch (status) {
        statusUnderReview => 'Under review',
        statusResolved => 'Resolved',
        statusRejected => 'Rejected',
        statusPending => 'Pending',
        _ => status,
      };

  String get reasonLabel => switch (reason) {
        'scope_creep' => 'Scope creep',
        'no_feedback' => 'No feedback',
        'Unlimited Revisions' => 'Unlimited revisions',
        'poor_quality' => 'Poor quality',
        'missed_deadline' => 'Missed deadline',
        'incomplete_work' => 'Incomplete work',
        'communication_issue' => 'Communication issue',
        _ => reason,
      };
}

/// Reads one half of `reviews: { client_review, freelancer_review }`.
ContractReview? reviewIn(Object? reviewsBox, String key) {
  final box = asObjectMap(reviewsBox);
  if (box == null) return null;
  final row = box[key];
  if (row is! Map) return null;
  return ContractReview.fromJson(row);
}

/// Reads `dispute_details`, tolerating the `null` every non-disputed contract
/// sends.
ContractDispute? readDispute(Object? row) {
  final box = asObjectMap(row);
  if (box == null) return null;
  return ContractDispute.fromJson(box);
}

/// `GET /api/contracts/` -> `{count, next, previous, results}`.
///
/// The paging envelope is **deliberate** — `ClientContracts.jsx` reads it — so
/// the list key is `next`, not the Laravel `next_page_url` the template used.
/// That mismatch is why `fetchNextPage` could only ever have fired with a null
/// URL and pagination was silently dead.
class ContractPage {
  final int count;
  final String? next;
  final String? previous;
  final List<Contract> results;

  const ContractPage({
    this.count = 0,
    this.next,
    this.previous,
    this.results = const [],
  });

  static const empty = ContractPage();

  bool get isEmpty => results.isEmpty;
  bool get hasNext => (next ?? '').isNotEmpty;

  /// Reads both the `{count, next, previous, results}` envelope and a bare
  /// array, so the same parser serves `GET /api/contracts/my_offers` (which
  /// returns a bare array) and the paged list.
  factory ContractPage.fromJson(dynamic payload) {
    if (payload is List) {
      return ContractPage(
        count: payload.length,
        results: asObjectList(payload, Contract.fromJson),
      );
    }
    if (payload is! Map) return empty;

    final j = Map<String, dynamic>.from(payload);
    final rows = j['results'] ?? j['data'] ?? j['orders'];
    return ContractPage(
      count: asInt(j['count']),
      next: asOptString(j['next']),
      previous: asOptString(j['previous']),
      results: asObjectList(rows, Contract.fromJson),
    );
  }
}
