import 'package:webofwork/customizations.dart';
import 'package:webofwork/helper/app_urls.dart';

/// The signed-in user's identity plus their freelancer profile.
///
/// This backend splits that across **two** endpoints, which is why the model
/// is built from two sources rather than one response:
///
///   * `GET /api/auth/me/`
///     -> `{ user: { id, full_name, email, profile_photo, ... } }`
///   * `GET /api/profile/freelancer/me/`
///     -> `{ id, tagline, bio, phone, experience_level, ... }`
///
/// The freelancer profile carries **no** name, email or photo. The model used
/// to expect a single `{ data: { first_name, last_name, ... } }` envelope from
/// the profile endpoint, which this backend never returns -- so `data` was
/// always `null`, and the ~20 call sites that gate on `data == null` rendered
/// a permanent `AccountSkeleton` (Contracts, Profile, Chat, Notifications) and
/// the dashboard greeting fell back to the literal "Freelancer".
class ProfileInfoModel {
  Data? data;

  ProfileInfoModel({this.data});

  /// [user] is the `user` object from `/api/auth/me/`; [profile] is the
  /// freelancer profile object. Either may be empty -- a partial fetch still
  /// produces a usable [Data] with whatever did arrive.
  factory ProfileInfoModel.fromSources(Map user, Map profile) =>
      ProfileInfoModel(data: Data.fromSources(user, profile));

  Map<String, dynamic> toJson() => {'data': data?.toJson()};
}

class Data {
  /// The **user** id, not the freelancer-profile id -- they are different
  /// Guids, and chat/message identity is keyed on the user id.
  String? id;
  String? firstName;
  String? lastName;
  String? email;
  String? phone;
  String? image;
  String? experienceLevel;

  /// Whether the account has a freelancer profile.
  ///
  /// Tri-state on purpose: `null` means "the server did not say", which is
  /// different from `false` meaning "it said no". This app is freelancer-only,
  /// and the splash uses that difference to decide whether to sign someone out --
  /// treating `null` as `false` would sign out every user whenever the profile
  /// request merely failed.
  bool? isFreelancer;

  /// Location is not modelled by this backend (the location editors were
  /// dropped), so these stay null. They are kept only because the profile-edit
  /// view and its service still reference them.
  dynamic country;
  dynamic countryId;
  dynamic state;
  dynamic stateId;
  dynamic city;
  dynamic cityId;

  Data({
    this.id,
    this.firstName,
    this.lastName,
    this.email,
    this.phone,
    this.image,
    this.experienceLevel,
    this.isFreelancer,
    this.country,
    this.countryId,
    this.state,
    this.stateId,
    this.city,
    this.cityId,
  });

  factory Data.fromSources(Map user, Map profile) {
    // "Xetuwiz Catacolit" -> firstName "Xetuwiz", lastName "Catacolit".
    final parts = _splitName((user['full_name'] ?? '').toString());

    return Data(
      id: user['id']?.toString(),
      firstName: parts.isEmpty ? null : parts.first,
      lastName: parts.length > 1 ? parts.sublist(1).join(' ') : null,
      email: user['email']?.toString(),
      image: resolveMediaUrl(user['profile_photo']?.toString()),
      phone: profile['phone']?.toString(),
      experienceLevel: profile['experience_level']?.toString(),
      // `/api/auth/me/` returns a JSON boolean here. Parsed strictly rather than
      // as truthiness: `user['is_freelancer'] == true` yields null for a
      // missing key, which is the "cannot prove otherwise" case the splash
      // relies on. A truthiness check would collapse that into false and sign
      // people out on a failed request.
      isFreelancer: user['is_freelancer'] is bool ? user['is_freelancer'] : null,
    );
  }

  static List<String> _splitName(String raw) {
    final value = raw.trim();
    if (value.isEmpty) return const [];
    return value.split(RegExp(r'\s+'));
  }

  Map<String, dynamic> toJson() => {
        'id': id,
        'first_name': firstName,
        'last_name': lastName,
        'email': email,
        'phone': phone,
        'image': image,
        'experience_level': experienceLevel,
        'country': country,
        'country_id': countryId,
        'state': state,
        'state_id': stateId,
        'city': city,
        'city_id': cityId,
      };
}

/// Accepts an absolute URL, a `/media/...` path, or a bare relative path and
/// returns something `Image.network` can load. Returns null rather than an
/// empty string so callers fall back to their initials placeholder.
String? resolveMediaUrl(String? raw) {
  if (raw == null) return null;
  final value = raw.trim();
  if (value.isEmpty) return null;
  if (value.startsWith('http://') || value.startsWith('https://')) return value;

  final relative = value.startsWith('/') ? value.substring(1) : value;
  if (relative.startsWith('media/')) return '$baseEndPoint/$relative';
  return '${AppUrls.profileImagePath}$relative';
}
