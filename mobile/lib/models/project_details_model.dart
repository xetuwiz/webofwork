import 'dart:convert';

import 'package:webofwork/helper/json_coercion.dart';

ProjectDetailsModel projectDetailsModelFromJson(String str) =>
    ProjectDetailsModel.fromJson(json.decode(str));

String projectDetailsModelToJson(ProjectDetailsModel data) => json.encode(data.toJson());

/// The freelancer-shaped project detail, as returned by
/// `GET /api/projects/{id}/details` (`ProjectsController.Details`).
///
/// ## Why this was rewritten
///
/// The template parsed a nested `job_details` object plus sibling `user`,
/// `image`, `hiring_rate`, `job_file_path` and `check_proposal_send_or_not`
/// keys. **None of those exist on this API.** The endpoint answers a single bare
/// `object` with `client_info` nested inside it, so `job_details` was always
/// null and the job-details screen rendered
/// "Project details not found" on *every* job.
///
/// `alreadyApplied` was worse than a blank screen. It was computed as
/// `json["check_proposal_send_or_not"].toString() != "0"`, and Dart renders a
/// missing key's value as the string `"null"` -- which is not `"0"`, so the
/// comparison was always true. Apply was therefore permanently disabled with
/// the label "Already proposed", on projects the freelancer had never applied to.
/// It now reads the real `is_applied` bool.
///
/// The template-only helper classes (`projectCreator`, `UserCountry`, `ProjectSkill`,
/// `JobProposal`, `Pivot`) are gone. `job_list_model.dart` declares its own
/// copies for the find-jobs list, and nothing imported them from here but
/// `ProjectDetailsService`.
class ProjectDetailsModel {
  ProjectDetails? projectDetails;

  ProjectDetailsModel({this.projectDetails});

  factory ProjectDetailsModel.fromJson(Map json) {
    // The endpoint answers a bare object, so that is the primary shape.
    //
    // The `job_details` / `data` fallbacks are pure defensiveness: they cost
    // nothing, and if anything ever re-wraps the payload (a proxy, a future
    // revision of the endpoint) this screen keeps rendering instead of silently
    // blanking to "Project details not found" -- which is exactly how this bug
    // presented, and why it survived so long.
    final wrapped = asObjectMap(json['job_details']) ??
        asObjectMap(json['data']) ??
        asObjectMap(json['project']);
    final body = wrapped ?? json;
    return ProjectDetailsModel(
      projectDetails: body.isEmpty ? null : ProjectDetails.fromJson(body),
    );
  }

  Map<String, dynamic> toJson() => projectDetails?.toJson() ?? <String, dynamic>{};
}

/// One project, as `ProjectsController.Details` sends it.
class ProjectDetails {
  /// `id` is the **project** id -- the value `POST /api/projects/create-proposal`
  /// and `GET /api/projects/{id}/details` both key on. It is deliberately not
  /// nullable: an Apply button with no project id can do nothing, and the old
  /// nullable `dynamic` forced a null check at every call site.
  int id;

  String title;
  String description;
  String requirements;
  String expectedDeliverables;

  /// `pricing_type` is emitted by `SnakeCase.Of(ProjectPricingType)`, so this is
  /// the literal string `"fixed"` or `"range"` -- never `1`/`2`.
  String pricingType;

  /// `fixed_price` for fixed-price projects; `null` for a range.
  double? fixedPrice;
  double? minBudget;
  double? maxBudget;

  /// `delivery_days`. The old model called this `duration` and formatted it into
  /// a `"14 days"` string at parse time, which made it un-comparable.
  int deliveryDays;

  /// `status`, snake_case: `open`, `in_progress`, `closed`, `completed`,
  /// `cancelled`.
  String status;

  bool isActive;

  /// `proposals_count`. The old model read a nested `job_proposals` array that
  /// this API never sends, so the count on every job card was always zero.
  int proposalsCount;

  /// `is_applied` -- has *this* freelancer already proposed on this project.
  bool isApplied;

  DateTime? createdAt;
  DateTime? updatedAt;

  int? categoryId;
  String categoryName;

  /// `skills` is a `List<string>` of names, not a list of objects. The reference
  /// `get_skills` returns names and the cards render each entry directly, so
  /// serialising objects here produced `[object Object]`.
  List<String> skills;

  /// The client who posted the job, or null when the server sent no
  /// `client_info` (`GET /api/projects/{id}` never populates it;
  /// `/{id}/details` does).
  ProjectDetailsClient? client;

  ProjectDetails({
    required this.id,
    this.title = '',
    this.description = '',
    this.requirements = '',
    this.expectedDeliverables = '',
    this.pricingType = 'fixed',
    this.fixedPrice,
    this.minBudget,
    this.maxBudget,
    this.deliveryDays = 0,
    this.status = '',
    this.isActive = true,
    this.proposalsCount = 0,
    this.isApplied = false,
    this.createdAt,
    this.updatedAt,
    this.categoryId,
    this.categoryName = '',
    this.skills = const [],
    this.client,
  });

  factory ProjectDetails.fromJson(Map json) {
    // `pricing_type` decides how the budget is read. Doing it once here means no
    // call site has to remember that a range project has no `fixed_price`.
    final pricingType = (json['pricing_type'] ?? '').toString().trim();
    final fixedPrice = json['fixed_price'] == null ? null : asDouble(json['fixed_price']);
    final minBudget = json['min_budget'] == null ? null : asDouble(json['min_budget']);
    final maxBudget = json['max_budget'] == null ? null : asDouble(json['max_budget']);

    return ProjectDetails(
      id: asInt(json['id']),
      title: (json['title'] ?? '').toString(),
      description: (json['description'] ?? '').toString(),
      requirements: (json['requirements'] ?? '').toString(),
      expectedDeliverables: (json['expected_deliverables'] ?? '').toString(),
      pricingType: pricingType.isEmpty ? 'fixed' : pricingType,
      fixedPrice: fixedPrice,
      minBudget: minBudget,
      maxBudget: maxBudget,
      deliveryDays: asInt(json['delivery_days']),
      status: (json['status'] ?? '').toString(),
      isActive: asBool(json['is_active']),
      proposalsCount: asInt(json['proposals_count']),
      isApplied: asBool(json['is_applied']),
      createdAt: asDateOrNull(json['created_at']),
      updatedAt: asDateOrNull(json['updated_at']),
      // The endpoint sends `category` (the id) and `category_id` is not present
      // on the anonymous-anonymous shape; accept either.
      categoryId: asIntOrNull(json['category'] ?? json['category_id']),
      categoryName: (json['category_name'] ?? '').toString(),
      skills: asStringList(json['skills']),
      client: ProjectDetailsClient.fromJsonOrNull(json['client_info']),
    );
  }

  /// True for a project with one fixed price rather than a min/max range.
  bool get isFixedPrice => pricingType != 'range';

  /// The single number a job card can show as "the price".
  ///
  /// A range project has no single price, so the midpoint is used -- the same
  /// choice `job_list_model.dart`'s `Project` makes, kept identical so the list card
  /// and the detail card cannot disagree about the same project.
  double? get displayPrice {
    if (isFixedPrice) return fixedPrice;
    if (minBudget != null && maxBudget != null) return (minBudget! + maxBudget!) / 2;
    return minBudget ?? maxBudget;
  }

  /// A human-readable budget range, for the detail screen where there is room
  /// to show both ends.
  String get budgetLabel {
    if (isFixedPrice) return fixedPrice?.toStringAsFixed(2) ?? '-';
    if (minBudget != null && maxBudget != null) {
      return '${minBudget!.toStringAsFixed(2)} - ${maxBudget!.toStringAsFixed(2)}';
    }
    return (minBudget ?? maxBudget)?.toStringAsFixed(2) ?? '-';
  }

  bool get isOpen => status == 'open';

  Map<String, dynamic> toJson() => {
        'id': id,
        'title': title,
        'description': description,
        'requirements': requirements,
        'expected_deliverables': expectedDeliverables,
        'pricing_type': pricingType,
        'fixed_price': fixedPrice,
        'min_budget': minBudget,
        'max_budget': maxBudget,
        'delivery_days': deliveryDays,
        'status': status,
        'is_active': isActive,
        'proposals_count': proposalsCount,
        'is_applied': isApplied,
        'created_at': createdAt?.toIso8601String(),
        'updated_at': updatedAt?.toIso8601String(),
        'category': categoryId,
        'category_name': categoryName,
        'skills': skills,
        'client_info': client?.toJson(),
      };
}

/// The `client_info` sub-object on `GET /api/projects/{id}/details`:
/// `{id, full_name, profile_photo, company_name, average_rating, review_count,
/// total_jobs_posted}`.
///
/// There is no location and no member-since on this payload. The old
/// "About the client" card rendered "Member since <today>" and a country of
/// `---` for every project, because it read the template's `created_at` and
/// `user_country` keys; both cards are now driven only by real fields.
class ProjectDetailsClient {
  /// The **client profile** id, not the user id.
  String id;

  String fullName;
  String? profilePhoto;
  String? companyName;

  /// `decimal` -> a JSON **string**, via `DecimalAsStringJsonConverter`.
  double averageRating;
  int reviewCount;
  int totalJobsPosted;

  ProjectDetailsClient({
    required this.id,
    this.fullName = '',
    this.profilePhoto,
    this.companyName,
    this.averageRating = 0,
    this.reviewCount = 0,
    this.totalJobsPosted = 0,
  });

  static ProjectDetailsClient? fromJsonOrNull(dynamic value) {
    final map = asObjectMap(value);
    if (map == null) return null;
    return ProjectDetailsClient(
      id: (map['id'] ?? '').toString(),
      fullName: (map['full_name'] ?? '').toString(),
      profilePhoto: asOptString(map['profile_photo']),
      companyName: asOptString(map['company_name']),
      averageRating: map['average_rating'] == null ? 0 : asDouble(map['average_rating']),
      reviewCount: asInt(map['review_count']),
      totalJobsPosted: asInt(map['total_jobs_posted']),
    );
  }

  /// The first word of the name, for the avatar placeholder.
  String get initial =>
      fullName.trim().isEmpty ? '' : fullName.trim().substring(0, 1).toUpperCase();

  Map<String, dynamic> toJson() => {
        'id': id,
        'full_name': fullName,
        'profile_photo': profilePhoto,
        'company_name': companyName,
        'average_rating': averageRating,
        'review_count': reviewCount,
        'total_jobs_posted': totalJobsPosted,
      };
}
