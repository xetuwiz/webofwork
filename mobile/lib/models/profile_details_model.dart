import 'dart:convert';

import '../helper/json_coercion.dart';
import 'profile_info_model.dart' show resolveMediaUrl;

ProfileDetailsModel profileDetailsModelFromJson(String str) =>
    ProfileDetailsModel.fromJson(json.decode(str));

String profileDetailsModelToJson(ProfileDetailsModel data) => json.encode(data.toJson());

/// Public view of the signed-in freelancer, from
/// `GET /api/profile/freelancer/profile` (ProfilesController.Big).
///
/// The wire shape is **flat** and snake_case:
///
/// ```json
/// { "full_name": "...", "profile_photo": "...", "tagline": "...", "bio": "...",
///   "phone": "...", "category": 3, "experience_level": "expert",
///   "availability": "...", "skills": ["React", "Figma"],
///   "packages": { "starter": {...}|null, "pro": {...}|null },
///   "portfolios": [ { "title", "description", "image_url" } ],
///   "payment_settings": { "paypal_email", "is_verified" } | null,
///   "created_at": "...", "completed_projects_count": 4,
///   "review_count": 2, "average_rating": 4.5, "reviews": [] }
/// ```
///
/// The previous model was template-shaped (`{data: {user: {user_introduction:
/// {description}}}}`, comma-joined `skills` string, `portfolio_path` + `image`,
/// plus `educations` / `experiences` / `projects` / `total_earning` /
/// `complete_orders`). None of those keys exist here, and `skills` is a JSON
/// **array**, so assigning it to the old `String? skills` field threw a cast
/// error on every successful 200 -- which is why this screen never rendered.
///
/// `availability`, `skills` and the packages are editable from
/// `PATCH /api/profile/freelancer/me` (see Profile Edit); this view is
/// read-only because there is no separate public-profile write route.
class ProfileDetailsModel {
  String? fullName;
  String? profilePhoto;
  String? tagline;
  String? bio;
  String? phone;
  bool isPhoneVerified;
  int? category;
  String? experienceLevel;
  String? availability;

  /// Flat list of skill names -- already resolved server-side, including custom
  /// ones (ProfilesController.Big joins `FreelancerSkills` to `Skills` and
  /// falls back to `CustomName`).
  List<String> skills;

  Package? starter;
  Package? pro;
  List<PortfolioItem> portfolios;
  PaymentSettings? paymentSettings;

  DateTime? createdAt;
  int completedProjectsCount;
  int reviewCount;

  /// `decimal` serialises as a JSON **string** on this API, so "4.5" is the
  /// expected shape, but a bare number is accepted too.
  double averageRating;

  /// `reviews` is hard-coded to an empty array by the backend today, so this is
  /// always empty. Modelled anyway so it starts working the moment the backend
  /// fills it in.
  List<ProfileReview> reviews;

  ProfileDetailsModel({
    this.fullName,
    this.profilePhoto,
    this.tagline,
    this.bio,
    this.phone,
    this.isPhoneVerified = false,
    this.category,
    this.experienceLevel,
    this.availability,
    List<String>? skills,
    this.starter,
    this.pro,
    List<PortfolioItem>? portfolios,
    this.paymentSettings,
    this.createdAt,
    this.completedProjectsCount = 0,
    this.reviewCount = 0,
    this.averageRating = 0,
    List<ProfileReview>? reviews,
  })  : skills = skills ?? const [],
        portfolios = portfolios ?? const [],
        reviews = reviews ?? const [];

  /// True once the payload had enough in it to be worth rendering, so the view
  /// can tell "still loading" from "the API returned nothing".
  bool get isPopulated => fullName != null || tagline != null || bio != null;

  factory ProfileDetailsModel.fromJson(Map json) {
    final packages = json['packages'];
    final packageMap =
        packages is Map ? Map<String, dynamic>.from(packages) : const <String, dynamic>{};

    List<PortfolioItem> parsePortfolios() {
      final raw = json['portfolios'];
      if (raw is! List) return const [];
      return raw
          .whereType<Map>()
          .map((e) => PortfolioItem.fromJson(Map<String, dynamic>.from(e)))
          .toList();
    }

    List<ProfileReview> parseReviews() {
      final raw = json['reviews'];
      if (raw is! List) return const [];
      return raw
          .whereType<Map>()
          .map((e) => ProfileReview.fromJson(Map<String, dynamic>.from(e)))
          .toList();
    }

    return ProfileDetailsModel(
      fullName: json['full_name']?.toString(),
      profilePhoto: resolveMediaUrl(json['profile_photo']?.toString()),
      tagline: _blankToNull(json['tagline']),
      bio: _blankToNull(json['bio']),
      phone: _blankToNull(json['phone']),
      isPhoneVerified: json['is_phone_verified'] == true,
      category: asIntOrNull(json['category']),
      experienceLevel: _blankToNull(json['experience_level']),
      availability: _blankToNull(json['availability']),
      skills: (json['skills'] is List)
          ? (json['skills'] as List)
              .map((e) => e.toString().trim())
              .where((e) => e.isNotEmpty)
              .toList()
          : const [],
      starter: _asPackage(packageMap['starter']),
      pro: _asPackage(packageMap['pro']),
      portfolios: parsePortfolios(),
      paymentSettings: json['payment_settings'] is Map
          ? PaymentSettings.fromJson(
              Map<String, dynamic>.from(json['payment_settings'] as Map))
          : null,
      createdAt: json['created_at'] == null
          ? null
          : DateTime.tryParse(json['created_at'].toString()),
      completedProjectsCount: asIntOrNull(json['completed_projects_count']) ?? 0,
      reviewCount: asIntOrNull(json['review_count']) ?? 0,
      averageRating: _asDouble(json['average_rating']) ?? 0,
      reviews: parseReviews(),
    );
  }

  static Package? _asPackage(dynamic raw) =>
      raw is Map ? Package.fromJson(Map<String, dynamic>.from(raw)) : null;

  static String? _blankToNull(dynamic v) {
    final s = v?.toString().trim();
    return (s == null || s.isEmpty) ? null : s;
  }

  static double? _asDouble(dynamic v) {
    if (v is num) return v.toDouble();
    return double.tryParse(v?.toString() ?? '');
  }

  Map<String, dynamic> toJson() => {
        'full_name': fullName,
        'profile_photo': profilePhoto,
        'tagline': tagline,
        'bio': bio,
        'phone': phone,
        'is_phone_verified': isPhoneVerified,
        'category': category,
        'experience_level': experienceLevel,
        'availability': availability,
        'skills': skills,
        'packages': {
          'starter': starter?.toJson(),
          'pro': pro?.toJson(),
        },
        'portfolios': portfolios.map((e) => e.toJson()).toList(),
        'payment_settings': paymentSettings?.toJson(),
        'created_at': createdAt?.toIso8601String(),
        'completed_projects_count': completedProjectsCount,
        'review_count': reviewCount,
        'average_rating': averageRating,
        'reviews': reviews.map((e) => e.toJson()).toList(),
      };
}

/// One of the two package tiers. `price` arrives as a decimal string
/// ("125.00"), which is why the converter exists on the server side.
class Package {
  int? id;
  double price;
  int deliveryDays;
  List<String> description;

  Package({
    this.id,
    this.price = 0,
    this.deliveryDays = 0,
    List<String>? description,
  }) : description = description ?? const [];

  factory Package.fromJson(Map<String, dynamic> json) {
    final raw = json['description'];
    // The backend splits the stored text on newlines, so this is a list -- but
    // tolerate a bare string from a hand-edited row.
    final lines = raw is List
        ? raw.map((e) => e.toString()).toList()
        : (raw?.toString().isNotEmpty == true ? raw.toString().split('\n') : <String>[]);
    return Package(
      id: asIntOrNull(json['id']),
      price: ProfileDetailsModel._asDouble(json['price']) ?? 0,
      deliveryDays: asIntOrNull(json['delivery_days']) ?? 0,
      description: lines,
    );
  }

  Map<String, dynamic> toJson() => {
        'id': id,
        'price': price,
        'delivery_days': deliveryDays,
        'description': description,
      };
}

class PortfolioItem {
  /// The row's Guid. Required to edit or delete it --
  /// `PATCH|DELETE /api/profile/freelancer/portfolio/{id}`. This field was
  /// absent, so the portfolio list was strictly read-only: there was no id to
  /// address in either request.
  String? id;
  String? title;
  String? description;
  String? imageUrl;

  PortfolioItem({this.id, this.title, this.description, this.imageUrl});

  factory PortfolioItem.fromJson(Map<String, dynamic> json) => PortfolioItem(
        id: json['id']?.toString(),
        title: json['title']?.toString(),
        description: json['description']?.toString(),
        // Key is `image_url`, not the template's `image`.
        imageUrl: resolveMediaUrl(json['image_url']?.toString()),
      );

  Map<String, dynamic> toJson() => {
        'id': id,
        'title': title,
        'description': description,
        'image_url': imageUrl,
      };
}

class PaymentSettings {
  String? paypalEmail;
  bool isVerified;

  PaymentSettings({this.paypalEmail, this.isVerified = false});

  factory PaymentSettings.fromJson(Map<String, dynamic> json) => PaymentSettings(
        paypalEmail: json['paypal_email']?.toString(),
        isVerified: json['is_verified'] == true,
      );

  Map<String, dynamic> toJson() => {
        'paypal_email': paypalEmail,
        'is_verified': isVerified,
      };
}

class ProfileReview {
  int? id;
  String? reviewerName;
  String? reviewerPhoto;
  double rating;
  String? comment;
  DateTime? createdAt;

  ProfileReview({
    this.id,
    this.reviewerName,
    this.reviewerPhoto,
    this.rating = 0,
    this.comment,
    this.createdAt,
  });

  factory ProfileReview.fromJson(Map<String, dynamic> json) => ProfileReview(
        id: asIntOrNull(json['id']),
        reviewerName: json['reviewer_name']?.toString() ??
            (json['reviewer'] is Map
                ? (json['reviewer']['full_name'] ?? json['reviewer']['name'])?.toString()
                : null),
        reviewerPhoto: resolveMediaUrl((json['reviewer_photo'] ??
                (json['reviewer'] is Map ? json['reviewer']['profile_photo'] : null))
            ?.toString()),
        rating: ProfileDetailsModel._asDouble(json['rating']) ?? 0,
        comment: json['comment']?.toString(),
        createdAt: json['created_at'] == null
            ? null
            : DateTime.tryParse(json['created_at'].toString()),
      );

  Map<String, dynamic> toJson() => {
        'id': id,
        'reviewer_name': reviewerName,
        'reviewer_photo': reviewerPhoto,
        'rating': rating,
        'comment': comment,
        'created_at': createdAt?.toIso8601String(),
      };
}
