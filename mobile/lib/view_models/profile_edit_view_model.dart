import 'package:flutter/material.dart';
import 'package:provider/provider.dart';
import 'package:webofwork/helper/local_keys.g.dart';
import 'package:webofwork/models/freelancer_profile_model.dart';
import 'package:webofwork/models/profile_details_model.dart' show PortfolioItem;
import 'package:webofwork/services/profile_edit_service.dart';

/// Form state for the professional profile editor.
///
/// The form used to be first/last name + email + phone + an experience
/// dropdown, and its submit handler was gated on `countryValidate &&
/// stateValidate && cityValidate` -- fields that do not exist on the form and
/// whose value is always null. Save therefore always returned early with
/// "Select a country" and never issued a request.
///
/// It now mirrors the fields the API actually stores: full name, tagline, bio,
/// category, experience level, availability, skills and the two service
/// packages. Email and phone are shown read-only because changing them is a
/// verified flow (`PUT /api/auth/me` plus an email OTP, and
/// `POST /api/profile/freelancer/send-phone-otp` plus verify) rather than a
/// field on this form.
class ProfileEditViewModel {
  /// Owned by the view's State, not by this singleton.
  GlobalKey<FormState>? formKey;

  /// True while the profile and categories are being fetched.
  final ValueNotifier<bool> loading = ValueNotifier<bool>(true);
  final ValueNotifier<bool> saving = ValueNotifier<bool>(false);

  final TextEditingController fullNameController = TextEditingController();
  final TextEditingController taglineController = TextEditingController();
  final TextEditingController bioController = TextEditingController();
  final TextEditingController skillController = TextEditingController();

  final TextEditingController starterPriceController = TextEditingController();
  final TextEditingController starterDaysController = TextEditingController();
  final TextEditingController starterDescriptionController = TextEditingController();

  final TextEditingController proPriceController = TextEditingController();
  final TextEditingController proDaysController = TextEditingController();
  final TextEditingController proDescriptionController = TextEditingController();

  final ValueNotifier<String?> experienceLevel = ValueNotifier<String?>(null);
  final ValueNotifier<bool> availability = ValueNotifier<bool>(false);
  final ValueNotifier<List<String>> skills = ValueNotifier<List<String>>([]);

  /// The freelancer's portfolio rows, with their `id`s.
  ///
  /// Held separately from the rest of the form because they are **not** saved by
  /// the Save button: `UpdateFreelancerProfileRequest` has no portfolio field, so
  /// they go through their own item endpoints and commit immediately.
  final ValueNotifier<List<PortfolioItem>> portfolios =
      ValueNotifier<List<PortfolioItem>>([]);

  /// True while a portfolio item is being written, so the editor sheet and the
  /// item buttons can disable themselves instead of firing a second request.
  final ValueNotifier<bool> portfolioBusy = ValueNotifier<bool>(false);
  final ValueNotifier<List<ProfileCategoryOption>> categories =
      ValueNotifier<List<ProfileCategoryOption>>([]);
  final ValueNotifier<ProfileCategoryOption?> selectedCategory =
      ValueNotifier<ProfileCategoryOption?>(null);

  ProfileEditViewModel._init();
  static ProfileEditViewModel? _instance;
  static ProfileEditViewModel get instance {
    _instance ??= ProfileEditViewModel._init();
    return _instance!;
  }

  static bool get dispose {
    _instance = null;
    return true;
  }

  /// Loads the current profile and the category list, then seeds the form.
  ///
  /// Called from the view's `initState`. `ProfileEditViewModel.instance` is a
  /// process-wide singleton, so a re-open re-runs this and re-seeds every
  /// controller rather than appending to whatever the last visit left behind.
  Future<void> initEdit(BuildContext context) async {
    loading.value = true;

    final service = Provider.of<ProfileEditService>(context, listen: false);

    // Independent requests, so run them together.
    final results = await Future.wait([
      service.fetchProfile(),
      service.fetchCategories(),
    ]);

    final profile = results[0] as FreelancerProfile?;
    final options = results[1] as List<ProfileCategoryOption>;

    categories.value = options;

    if (profile == null) {
      // The network layer has already shown the error. Still stop the spinner so
      // the form is usable rather than stuck behind a loading state.
      loading.value = false;
      return;
    }

    _seed(profile, options);
    loading.value = false;
  }

  /// Copies [profile] into the form. Also used after a save to reflect exactly
  /// what the server stored.
  void _seed(FreelancerProfile profile, List<ProfileCategoryOption> options) {
    fullNameController.text = profile.fullName;
    taglineController.text = profile.tagline;
    bioController.text = profile.bio;

    experienceLevel.value = FreelancerProfile.isValidExperienceLevel(
      profile.experienceLevel,
    )
        ? profile.experienceLevel
        : null;
    availability.value = profile.availability;
    skills.value = List<String>.from(profile.skills);
    portfolios.value = List<PortfolioItem>.from(profile.portfolios);

    selectedCategory.value = _categoryWithId(options, profile.categoryId);

    _seedTier(
      profile.starter,
      starterPriceController,
      starterDaysController,
      starterDescriptionController,
    );
    _seedTier(
      profile.pro,
      proPriceController,
      proDaysController,
      proDescriptionController,
    );
  }

  /// Resolves the stored category id against the freshly loaded options. The id
  /// is the source of truth because a category can be renamed after it was set.
  static ProfileCategoryOption? _categoryWithId(
    List<ProfileCategoryOption> options,
    int? id,
  ) {
    if (id == null) return null;
    for (final option in options) {
      if (option.id == id) return option;
    }
    return null;
  }

  static void _seedTier(
    PackageTier? tier,
    TextEditingController price,
    TextEditingController days,
    TextEditingController description,
  ) {
    price.text = tier == null || tier.price <= 0 ? '' : _trimNumber(tier.price);
    days.text =
        tier == null || tier.deliveryDays <= 0 ? '' : tier.deliveryDays.toString();
    description.text = tier?.description ?? '';
  }

  /// `150.0` reads badly in a price field; `150` does not look like money
  /// either, so keep at most two decimals and no trailing zeros.
  static String _trimNumber(double value) {
    if (value == value.roundToDouble()) return value.toStringAsFixed(0);
    return value.toStringAsFixed(2);
  }

  // ── Skills ────────────────────────────────────────────────────────────────────

  /// Adds a skill. Blank and duplicate entries are ignored -- the server matches
  /// skills by name, so a repeated tag would just create a duplicate row.
  void addSkill(String raw) {
    final value = raw.trim();
    skillController.clear();
    if (value.isEmpty) return;

    final existing = skills.value;
    final alreadyThere = existing.any(
      (skill) => skill.toLowerCase() == value.toLowerCase(),
    );
    if (alreadyThere) return;

    skills.value = [...existing, value];
  }

  void removeSkill(String value) {
    skills.value = skills.value.where((skill) => skill != value).toList();
  }

  // ── Portfolio ─────────────────────────────────────────────────────────────────

  /// Creates a row and re-seeds the list from the server response.
  ///
  /// `POST` answers with only `{ id }`, so the refetch is what actually adds the
  /// row -- and it is the only way the new `id` becomes available for a later
  /// edit or delete.
  Future<bool> tryAddPortfolio(
    BuildContext context, {
    required String title,
    required String description,
    required String imageUrl,
  }) async {
    if (portfolioBusy.value) return false;
    portfolioBusy.value = true;
    try {
      final service = Provider.of<ProfileEditService>(context, listen: false);
      final ok = await service.tryCreatePortfolio(
        context,
        title: title,
        description: description,
        imageUrl: imageUrl,
      );
      if (ok) await _reseedPortfolios(context, service);
      return ok;
    } finally {
      portfolioBusy.value = false;
    }
  }

  /// PATCHes an existing row in place. [id] is the Guid from the list, which is
  /// why `PortfolioItem.id` has to survive the round trip.
  Future<bool> tryEditPortfolio(
    BuildContext context,
    String id, {
    required String title,
    required String description,
    required String imageUrl,
  }) async {
    if (portfolioBusy.value) return false;
    portfolioBusy.value = true;
    try {
      final service = Provider.of<ProfileEditService>(context, listen: false);
      final ok = await service.tryUpdatePortfolio(
        context,
        id,
        title: title,
        description: description,
        imageUrl: imageUrl,
      );
      if (ok) await _reseedPortfolios(context, service);
      return ok;
    } finally {
      portfolioBusy.value = false;
    }
  }

  Future<bool> tryDeletePortfolio(BuildContext context, String id) async {
    if (portfolioBusy.value) return false;
    portfolioBusy.value = true;
    try {
      final service = Provider.of<ProfileEditService>(context, listen: false);
      final ok = await service.tryDeletePortfolio(context, id);
      if (ok) await _reseedPortfolios(context, service);
      return ok;
    } finally {
      portfolioBusy.value = false;
    }
  }

  /// Refreshes [portfolios] from the server after a write.
  ///
  /// Deliberately re-reads instead of patching the local list: `POST` returns
  /// only an id, and the server trims and may reject fields, so the row the
  /// server stored is the only trustworthy copy.
  Future<void> _reseedPortfolios(
    BuildContext context,
    ProfileEditService service,
  ) async {
    final refreshed = await service.fetchProfile();
    if (refreshed == null) return;
    portfolios.value = List<PortfolioItem>.from(refreshed.portfolios);
  }

  // ── Validation ────────────────────────────────────────────────────────────────

  String? validateFullName(String? value) {
    if ((value ?? '').trim().length < 2) return LocalKeys.enterValidName;
    return null;
  }

  String? validateTagline(String? value) {
    // UpdateFreelancerProfileValidator caps the tagline at 100 characters.
    if ((value ?? '').trim().length > 100) return 'Maximum 100 characters';
    return null;
  }

  String? validateBio(String? value) {
    // UpdateFreelancerProfileValidator caps the bio at 500 characters.
    if ((value ?? '').trim().length > 500) return 'Maximum 500 characters';
    return null;
  }

  String? validatePrice(String? value) {
    final text = (value ?? '').trim();
    if (text.isEmpty) return null; // tier is simply not included in the payload
    final parsed = double.tryParse(text);
    if (parsed == null || parsed < 0) return 'Enter a valid price';
    return null;
  }

  String? validateDeliveryDays(String? value) {
    final text = (value ?? '').trim();
    if (text.isEmpty) return null;
    final parsed = int.tryParse(text);
    if (parsed == null || parsed < 1) return 'Enter a valid number of days';
    return null;
  }

  /// Assembles the model to be written. Public so the save path and any future
  /// preview share one definition of "what the form currently says".
  FreelancerProfile buildProfile() {
    return FreelancerProfile()
      ..fullName = fullNameController.text
      ..tagline = taglineController.text
      ..bio = bioController.text
      ..experienceLevel = experienceLevel.value ?? ''
      ..availability = availability.value
      ..skills = List<String>.from(skills.value)
      ..categoryId = selectedCategory.value?.id
      ..starter = _tierFrom(
        starterPriceController,
        starterDaysController,
        starterDescriptionController,
      )
      ..pro = _tierFrom(
        proPriceController,
        proDaysController,
        proDescriptionController,
      );
  }

  static PackageTier _tierFrom(
    TextEditingController price,
    TextEditingController days,
    TextEditingController description,
  ) {
    return PackageTier(
      price: double.tryParse(price.text.trim()) ?? 0,
      deliveryDays: int.tryParse(days.text.trim()) ?? 0,
      description: description.text,
    );
  }

  Future<void> updateProfileInfo(BuildContext context) async {
    if (saving.value) return;
    if (!(formKey?.currentState?.validate() ?? false)) return;

    saving.value = true;
    try {
      final service = Provider.of<ProfileEditService>(context, listen: false);
      final saved = await service.tryUpdateProfile(context, buildProfile());

      if (!saved) return;

      // Re-read so the form shows what the server actually persisted rather than
      // what was typed -- the API silently ignores a package with no price and
      // any empty name, and the user should see that outcome.
      final refreshed = await service.fetchProfile();
      if (refreshed != null) _seed(refreshed, categories.value);
    } finally {
      saving.value = false;
    }
  }
}
