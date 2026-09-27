import 'package:webofwork/helper/json_coercion.dart';

export 'package:webofwork/helper/json_coercion.dart';

/// My own service packages, read from
/// `GET /api/profile/freelancer/profile` -> `packages`.
///
/// That endpoint returns a **tier-keyed object**, not a list:
///
/// ```json
/// "packages": {
///   "starter": { "id": "…uuid…", "package_type": "starter",
///                "price": "250.00", "delivery_days": 7,
///                "description": ["line one", "line two"] },
///   "pro":     null
/// }
/// ```
///
/// A tier the freelancer has not created is **`null`**, not absent, so each
/// entry is null-checked before it is read. `description` arrives already split
/// on newlines, so it is a list of bullet points rather than a blob.
///
/// Packages with no usable `id` are dropped: the API rejects a proposal whose
/// package it cannot resolve, and showing an unselectable card is worse than
/// reporting none.
class MyPackages {
  final Map<String, FreelancerPackage> tiers;

  const MyPackages({this.tiers = const {}});

  static const empty = MyPackages();

  factory MyPackages.fromProfile(Map profile) {
    final raw = profile['packages'];
    if (raw is! Map) return empty;

    final tiers = <String, FreelancerPackage>{};
    // Fixed order so the cheaper tier is always offered first.
    for (final key in const ['starter', 'pro']) {
      final value = raw[key];
      if (value is! Map) continue;
      final pkg = FreelancerPackage.fromJson(Map<String, dynamic>.from(value));
      if (pkg.id.isEmpty) continue;
      tiers[key] = pkg;
    }
    return MyPackages(tiers: Map.unmodifiable(tiers));
  }

  /// Packages the user actually owns, `starter` then `pro`.
  List<FreelancerPackage> get all => const ['starter', 'pro']
      .map((k) => tiers[k])
      .whereType<FreelancerPackage>()
      .toList(growable: false);

  bool get isEmpty => all.isEmpty;

  bool get isNotEmpty => all.isNotEmpty;

  /// The tier shown pre-selected: `starter` when it exists, otherwise whatever
  /// single tier they do have.
  FreelancerPackage? get defaultTier =>
      tiers['starter'] ?? (all.isEmpty ? null : all.first);
}

class FreelancerPackage {
  /// Kept as the raw uuid string rather than a parsed `Guid`: the value has to
  /// round-trip into the proposal payload byte-for-byte.
  final String id;
  final String packageType;
  final double price;
  final int deliveryDays;
  final List<String> description;

  const FreelancerPackage({
    required this.id,
    required this.packageType,
    required this.price,
    required this.deliveryDays,
    this.description = const [],
  });

  factory FreelancerPackage.fromJson(Map<String, dynamic> json) => FreelancerPackage(
        id: (json['id'] ?? '').toString(),
        packageType: (json['package_type'] ?? '').toString(),
        price: asDouble(json['price']),
        deliveryDays: asInt(json['delivery_days']),
        description: asStringList(json['description']),
      );

  /// "Standard" / "Premium" for display. `package_type` is the lowercase key.
  String get label => switch (packageType) {
        'starter' => 'Standard',
        'pro' => 'Premium',
        _ => packageType,
      };
}

/// `POST /api/projects/create-proposal` -> a bare `ProposalResponse`.
///
/// ```json
/// { "id": 17, "project_id": 4, "project_title": "…",
///   "freelancer_id": "…uuid…", "package_id": "…uuid…",
///   "cover_letter": "…", "bid_amount": "250.00",
///   "delivery_days": 7, "status": "pending", "created_at": "…" }
/// ```
///
/// Read back so the sheet can confirm what was actually stored: `bid_amount` and
/// `delivery_days` are the **package's** values, since
/// `ProposalService.CreateProposalAsync` overwrites both with the package's own
/// price and duration and ignores whatever the form sent.
class SubmittedProposal {
  final int? id;
  final int? projectId;
  final String status;
  final String bidAmount;
  final int deliveryDays;

  const SubmittedProposal({
    this.id,
    this.projectId,
    this.status = '',
    this.bidAmount = '',
    this.deliveryDays = 0,
  });

  static SubmittedProposal? tryFrom(dynamic payload) {
    if (payload is! Map) return null;
    return SubmittedProposal.fromJson(Map<String, dynamic>.from(payload));
  }

  factory SubmittedProposal.fromJson(Map<String, dynamic> json) => SubmittedProposal(
        id: asIntOrNull(json['id']),
        projectId: asIntOrNull(json['project_id']),
        status: (json['status'] ?? '').toString(),
        bidAmount: asDouble(json['bid_amount']).toStringAsFixed(0),
        deliveryDays: asInt(json['delivery_days']),
      );
}
