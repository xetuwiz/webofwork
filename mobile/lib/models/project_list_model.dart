import 'dart:convert';

import 'package:webofwork/helper/json_coercion.dart';

ProjectListModel projectListModelFromJson(String str) => ProjectListModel.fromJson(json.decode(str));

String projectListModelToJson(ProjectListModel data) => json.encode(data.toJson());

class ProjectListModel {
  Projects? projects;

  ProjectListModel({
    this.projects,
  });

  factory ProjectListModel.fromJson(Map json) => ProjectListModel(
        // WebOfWork returns the page itself: `{results, count, page, page_size}`.
        // The template nested the same data under a `jobs` key. Both are accepted:
        // the server shape is primary, the wrapper is a harmless fallback.
        projects: (json['results'] == null && json['jobs'] == null)
            ? null
            : Projects.fromJson(asObjectMap(json['jobs']) ?? json),
      );

  Map<String, dynamic> toJson() => {
        'jobs': projects?.toJson(),
      };
}

class Projects {
  List<Project>? projects;

  /// `count` — the server's total for the whole query, not the page size.
  ///
  /// This is what drives paging. There is deliberately no `next` URL field:
  /// `ProjectsController.FreelancerList` answers
  /// `{results, count, page, page_size, total_pages}` and nothing else. The
  /// template read `json["next"]`, which was therefore permanently null, and
  /// `ProjectListService.fetchNextPage` bailed on its first line — so Find Projects
  /// could only ever show the first 20 open projects no matter how many existed.
  /// Paging is driven off `page` / `count` instead (same convention as
  /// `MyContractsListService` and `TicketListService`).
  int? total;

  /// `page` — 1-based, as echoed back by the server.
  int? page;

  /// `page_size` — the size the server actually used, after its
  /// `Math.Clamp(page_size, 1, 100)`. Differs from the requested value whenever
  /// the client asks for more than 100.
  int? pageSize;

  /// `total_pages`.
  int? totalPages;

  Projects({
    this.projects,
    this.total,
    this.page,
    this.pageSize,
    this.totalPages,
  });

  factory Projects.fromJson(Map json) {
    // WebOfWork paginates with a top-level `results` array; the template
    // expected Laravel's nested `data`. Accept either shape.
    final rows = json['results'] ?? json['data'];
    return Projects(
      projects: rows == null ? [] : List<Project>.from(asObjectList(rows, Project.fromJson)),
      total: asIntOrNull(json['count']),
      page: asIntOrNull(json['page']),
      pageSize: asIntOrNull(json['page_size']),
      totalPages: asIntOrNull(json['total_pages']),
    );
  }

  Map<String, dynamic> toJson() => {
        'results': projects == null ? [] : List<dynamic>.from(projects!.map((x) => x.toJson())),
        'count': total,
        'page': page,
        'page_size': pageSize,
        'total_pages': totalPages,
      };
}

/// One project row from `GET /api/projects/freelancer/project-list` and
/// `GET /api/profile/freelancer/recommended-projects`, both of which answer
/// `ProjectResponse`.
///
/// ## What was removed and why
///
/// The template carried a `projectCreator` sub-object and its own `projectCreator`,
/// `UserCountry` and `Pivot` classes — around 240 lines modelling the template's user
/// columns this API does not have (`is_suspend`, `google_2fa_secret`,
/// `facebook_id`, `terms_condition`, `check_online_status`, `user_country`, a
/// `pivot` join row, and more).
///
/// That was not merely dead weight. Because `Project.fromJson` never populates
/// `projectCreator`, the find-jobs card was rendering
/// `projectCreator?.userCountry?.country ?? "----"` — so **every job in the app
/// showed a literal `----` as its location** — and
/// `projectCreator?.userVerifiedStatus ?? false`, printing "Not verified" about a
/// real client. Both were fabrications produced by an unfillable model. The
/// dashboard's recommendation card went further and passed a literal
/// `location: 'Remote'`.
///
/// `proposals_count` is gone from this model for the same reason: neither of
/// these two endpoints sends a proposal count, so the old
/// `projectProposalCount ?? 0` rendered "Proposals: 0" — asserting as fact that
/// nobody had bid. The job-details screen, whose endpoint *does* count, reads
/// `ProjectDetails.proposalsCount` instead.
class Project {
  int id;
  String title;
  String description;

  /// `category` is the **id**; `categoryName` is the display name.
  int? categoryId;
  String categoryName;

  /// `pricing_type`: `"fixed"` or `"range"`.
  String pricingType;

  /// The single number the card shows. Fixed projects use `fixed_price`;
  /// range projects use the midpoint of `min_budget`/`max_budget`, which is the
  /// same choice `ProjectDetails.displayPrice` makes — kept identical so the list
  /// card and the detail card cannot disagree about the same project.
  int? budget;

  /// `delivery_days`, pre-formatted for the card's duration slot.
  String? duration;

  /// `status`, snake_case: `open`, `in_progress`, `closed`, `completed`,
  /// `cancelled`.
  String status;

  DateTime? createdAt;
  DateTime? updatedAt;

  /// Skill names, from the `List<string>` the endpoint sends. Not objects —
  /// the reference `get_skills` returns names and the card renders each entry
  /// directly, so a list of objects rendered as `[object Object]`.
  List<ProjectSkill> projectSkills;

  Project({
    required this.id,
    this.title = '',
    this.description = '',
    this.categoryId,
    this.categoryName = '',
    this.pricingType = 'fixed',
    this.budget,
    this.duration,
    this.status = '',
    this.createdAt,
    this.updatedAt,
    this.projectSkills = const [],
  });

  factory Project.fromJson(Map json) {
    // `pricing_type` decides how the budget is read. Doing it once here means no
    // call site has to remember that a range project has no `fixed_price`.
    final pricingType = (json['pricing_type'] ?? '').toString().trim();
    final isRange = pricingType == 'range';

    int? budget;
    if (!isRange) {
      budget = json['fixed_price'] == null ? null : asInt(json['fixed_price']);
    } else {
      final min = json['min_budget'] == null ? null : asDouble(json['min_budget']);
      final max = json['max_budget'] == null ? null : asDouble(json['max_budget']);
      if (min != null && max != null) {
        budget = ((min + max) / 2).round();
      } else {
        budget = (min ?? max)?.round();
      }
    }

    return Project(
      id: asInt(json['id']),
      title: (json['title'] ?? '').toString(),
      description: (json['description'] ?? '').toString(),
      categoryId: asIntOrNull(json['category'] ?? json['category_id']),
      categoryName: (json['category_name'] ?? '').toString(),
      pricingType: pricingType.isEmpty ? 'fixed' : pricingType,
      budget: budget,
      // The card shows this next to the price as a duration label. Null when the
      // project has no delivery window, so the row is omitted rather than
      // rendered as "null days".
      duration: json['delivery_days'] == null ? null : '${json['delivery_days']} days',
      status: (json['status'] ?? '').toString(),
      createdAt: _tryParseDate(json['created_at']),
      updatedAt: _tryParseDate(json['updated_at']),
      projectSkills:
          asStringList(json['skills']).map((name) => ProjectSkill(skill: name)).toList(),
    );
  }

  /// WebOfWork emits some dates as `"MM/dd/yyyy HH:mm:ss"`, which
  /// `DateTime.parse` rejects. Rather than throwing inside a factory — which
  /// would take down the whole page, not just one row — return null and let the
  /// card omit the date.
  static DateTime? _tryParseDate(dynamic value) {
    if (value == null) return null;
    final raw = value.toString();
    final parsed = asDateOrNull(raw);
    if (parsed != null) return parsed;

    final match = RegExp(r'^(\d{1,2})/(\d{1,2})/(\d{4})').firstMatch(raw);
    if (match == null) return null;
    return DateTime(
      int.parse(match.group(3)!),
      int.parse(match.group(1)!),
      int.parse(match.group(2)!),
    );
  }

  Map<String, dynamic> toJson() => {
        'id': id,
        'title': title,
        'description': description,
        'category': categoryId,
        'category_name': categoryName,
        'pricing_type': pricingType,
        'fixed_price': budget,
        'delivery_days': duration,
        'status': status,
        'created_at': createdAt?.toIso8601String(),
        'updated_at': updatedAt?.toIso8601String(),
        'skills': projectSkills.map((s) => s.skill).toList(),
      };
}

/// A skill name attached to a project.
///
/// The template's version modelled an `id`, `category_id`, `sub_category_id`,
/// `status`, timestamps and a `Pivot` join row. This API has no sub-categories
/// at all and sends skills as bare strings, so the only thing that exists is the
/// name.
class ProjectSkill {
  final String skill;

  const ProjectSkill({this.skill = ''});

  factory ProjectSkill.fromJson(Map json) =>
      ProjectSkill(skill: (json['skill'] ?? json['name'] ?? '').toString());

  Map<String, dynamic> toJson() => {'skill': skill};

  @override
  bool operator ==(Object other) => other is ProjectSkill && other.skill == skill;

  @override
  int get hashCode => skill.hashCode;
}
