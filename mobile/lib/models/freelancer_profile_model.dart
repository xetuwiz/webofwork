import '../helper/json_coercion.dart';
import 'category_model.dart';
import 'profile_details_model.dart' show PortfolioItem;

/// The freelancer profile as the API actually models it.
///
/// Read from `GET /api/profile/freelancer/profile/`, which returns the whole
/// editable record in one object:
///
/// ```json
/// {
///   "full_name": "...", "tagline": "...", "bio": "...",
///   "phone": "...", "category": 1, "experience_level": "intermediate",
///   "availability": true, "skills": ["React", ".NET"],
///   "packages": {
///     "starter": { "id": "…", "price": "150.00", "delivery_days": 3,
///                  "description": ["line one", "line two"] },
///     "pro": null
///   }
/// }
/// ```
///
/// Two response quirks worth knowing, both handled here:
///
///  * **Every `decimal` is serialized as a string** by the server's
///    `DecimalAsStringJsonConverter`, so `price` arrives as `"150.00"` and
///    `average_rating` as `"0.00"` -- not as a number.
///  * A package `description` is stored as one newline-delimited string but read
///    back **split into a list of lines**.
///
/// On the way in, `price` must be sent as a *string* too. The same converter's
/// reader calls `reader.GetString()`, which throws on a JSON number and turns
/// the whole request into a 400.
class FreelancerProfile {
  String fullName = '';
  String? profilePhoto;
  String tagline = '';
  String bio = '';
  String? phone;
  int? categoryId;
  String experienceLevel = '';
  bool availability = false;
  List<String> skills = [];
  PackageTier? starter;
  PackageTier? pro;

  /// Portfolio rows, with their `id` so they can be edited or deleted.
  ///
  /// Deliberately **not** part of [toUpdatePayload]: `UpdateFreelancerProfileRequest`
  /// has no portfolio field, so sending them made the save claim work it then
  /// discarded. They are written through their own
  /// `POST|PATCH|DELETE /api/profile/freelancer/portfolio[/{id}]` routes.
  List<PortfolioItem> portfolios = [];

  FreelancerProfile();

  // ── Experience levels ────────────────────────────────────────────────────────
  // The API accepts exactly these three strings. The old form offered
  // "junior"/"midLevel"/"senior" display labels instead, so every save failed
  // validation and the stored value ("intermediate") never matched an option,
  // leaving the dropdown blank.

  static const String levelBeginner = 'beginner';
  static const String levelIntermediate = 'intermediate';
  static const String levelExpert = 'expert';

  static const List<String> experienceLevels = [
    levelBeginner,
    levelIntermediate,
    levelExpert,
  ];

  /// True when [value] is one the API will accept. An unrecognised level is
  /// dropped from the payload rather than sent and rejected with a 422.
  static bool isValidExperienceLevel(String? value) =>
      value != null && experienceLevels.contains(value);

  factory FreelancerProfile.fromJson(Map json) {
    final packages = json['packages'];
    final packageMap = packages is Map ? packages : const {};
    return FreelancerProfile()
      ..fullName = (json['full_name'] ?? '').toString()
      ..profilePhoto = json['profile_photo']?.toString()
      ..tagline = (json['tagline'] ?? '').toString()
      ..bio = (json['bio'] ?? '').toString()
      ..phone = json['phone']?.toString()
      ..categoryId = asIntOrNull(json['category'])
      ..experienceLevel = (json['experience_level'] ?? '').toString()
      ..availability = json['availability'] == true
      ..skills = _asStringList(json['skills'])
      ..starter = _asTier(packageMap['starter'])
      ..pro = _asTier(packageMap['pro'])
      ..portfolios = _asPortfolios(json['portfolios']);
  }

  static List<PortfolioItem> _asPortfolios(dynamic value) {
    if (value is! List) return [];
    return value
        .whereType<Map>()
        .map((e) => PortfolioItem.fromJson(Map<String, dynamic>.from(e)))
        .toList();
  }

  static PackageTier? _asTier(dynamic value) {
    if (value is! Map) return null;
    return PackageTier(
      price: _asDouble(value['price']),
      deliveryDays: asIntOrNull(value['delivery_days']) ?? 0,
      description: _joinLines(value['description']),
    );
  }

  /// The API returns a package description as a list of lines.
  static String _joinLines(dynamic value) {
    if (value is List) {
      return value.map((e) => e.toString()).join('\n');
    }
    return (value ?? '').toString();
  }

  static double _asDouble(dynamic value) {
    if (value is num) return value.toDouble();
    return double.tryParse((value ?? '').toString()) ?? 0;
  }

  static List<String> _asStringList(dynamic value) {
    if (value is! List) return [];
    return value.map((e) => e.toString().trim()).where((e) => e.isNotEmpty).toList();
  }

  /// Builds the JSON body for `PATCH /api/profile/freelancer/me`.
  ///
  /// The old client POSTed multipart form fields named `first_name`, `last_name`,
  /// `level`, `country`, `state` and `city` to a GET-only route, so every save
  /// returned 405 and nothing persisted. The real DTO
  /// (`UpdateFreelancerProfileRequest`) is a JSON body of `full_name`,
  /// `tagline`, `bio`, `experience_level`, `availability`, `skills`,
  /// `category_id` and `packages` -- and notably carries no `phone` and no
  /// location at all, so those cannot be saved from this form.
  ///
  /// Two server rules shape what is safe to send:
  ///
  ///  * `FullName` is applied whenever it is non-null, so an empty string would
  ///    blank the user's name. Omitted when blank.
  ///  * A package is created on demand, and its price is only overwritten when
  ///    `> 0`. Sending a tier with a zero price would therefore create an empty
  ///    package for someone who never had one, so a tier is included only once it
  ///    has a real price.
  Map<String, dynamic> toUpdatePayload() {
    final payload = <String, dynamic>{};

    final name = fullName.trim();
    if (name.isNotEmpty) payload['full_name'] = name;

    payload['tagline'] = tagline.trim();
    payload['bio'] = bio.trim();
    payload['availability'] = availability;

    if (isValidExperienceLevel(experienceLevel)) {
      payload['experience_level'] = experienceLevel;
    }

    // Always the complete list: the server replaces the set wholesale, so an
    // empty list is how the user clears their last skill.
    payload['skills'] = _dedupe(skills);

    if (categoryId != null) payload['category_id'] = categoryId;

    final packages = <String, dynamic>{};
    if (starter != null && starter!.isPriced) {
      packages['starter'] = starter!.toPayload();
    }
    if (pro != null && pro!.isPriced) {
      packages['pro'] = pro!.toPayload();
    }
    if (packages.isNotEmpty) payload['packages'] = packages;

    return payload;
  }

  /// Server-side skills are matched by name and de-duplicated case-insensitively;
  /// mirroring that here keeps the list clean before it is sent.
  static List<String> _dedupe(List<String> values) {
    final seen = <String>{};
    final out = <String>[];
    for (final value in values) {
      final trimmed = value.trim();
      if (trimmed.isEmpty) continue;
      if (seen.add(trimmed.toLowerCase())) out.add(trimmed);
    }
    return out;
  }
}

/// One service package tier ("starter" or "pro").
class PackageTier {
  double price;
  int deliveryDays;
  String description;

  PackageTier({
    this.price = 0,
    this.deliveryDays = 0,
    this.description = '',
  });

  /// A tier with no price is not a package, so it is left out of the payload
  /// entirely rather than creating an empty row server-side.
  bool get isPriced => price > 0;

  Map<String, dynamic> toPayload() => {
        // Sent as a string: the server's decimal converter reads with
        // `reader.GetString()`, which throws on a JSON number.
        'price': price.toStringAsFixed(2),
        'delivery_days': deliveryDays,
        'description': description.trim(),
      };
}

/// A category option for the dropdown, from `GET /api/categories/`.
class ProfileCategoryOption {
  final int id;
  final String name;

  const ProfileCategoryOption({required this.id, required this.name});

  /// Accepts a bare array (what `?no_pagination=true` returns) or the paginated
  /// `{count, results}` envelope, so the caller cannot get this wrong.
  static List<ProfileCategoryOption> listFromResponse(dynamic response) {
    dynamic raw = response;
    if (response is Map) raw = response['results'] ?? response['data'];

    if (raw is! List) return const [];

    final options = <ProfileCategoryOption>[];
    for (final item in raw) {
      // A malformed entry must not take the whole dropdown down with it.
      if (item is! Map) continue;

      // Reuse the existing Category model so ids/names stay consistent with the
      // rest of the app.
      final category = Category.fromJson(Map<String, dynamic>.from(item));
      final id = category.id is int ? category.id as int : int.tryParse('${category.id}');
      final name = (category.name ?? '').toString().trim();
      if (id != null && name.isNotEmpty) {
        options.add(ProfileCategoryOption(id: id, name: name));
      }
    }
    return options;
  }
}
