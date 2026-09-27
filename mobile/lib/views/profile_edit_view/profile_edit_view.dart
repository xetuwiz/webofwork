import 'package:flutter/material.dart';
import 'package:webofwork/helper/extension/context_extension.dart';
import 'package:webofwork/helper/extension/int_extension.dart';
import 'package:webofwork/helper/local_keys.g.dart';
import 'package:webofwork/models/freelancer_profile_model.dart';
import 'package:webofwork/utils/components/custom_button.dart';
import 'package:webofwork/utils/components/custom_preloader.dart';
import 'package:webofwork/view_models/profile_edit_view_model.dart';
import 'package:webofwork/views/profile_edit_view/components/category_dropdown_field.dart';
import 'package:webofwork/views/profile_edit_view/components/package_tier_section.dart';
import 'package:webofwork/views/profile_edit_view/components/portfolio_section.dart';
import 'package:webofwork/views/profile_edit_view/components/skill_tags_input.dart';
import 'package:webofwork/views/profile_settings_view/components/profile_info_avatar.dart';
import '/helper/extension/string_extension.dart';

import '../../utils/components/field_label.dart';
import '../../utils/components/field_with_label.dart';
import '../../utils/components/navigation_pop_icon.dart';

/// Editor for the freelancer's professional profile.
///
/// Every field here is one the API actually persists. The previous form showed
/// first/last name, email and phone, plus an experience dropdown whose options
/// ("junior", "midLevel", "senior") the API does not accept; it also refused to
/// submit at all, because the save handler required a country/state/city that
/// the form does not have.
class ProfileEditView extends StatefulWidget {
  static const routeName = 'profile_edit_view';
  const ProfileEditView({super.key});

  @override
  State<ProfileEditView> createState() => _ProfileEditViewState();
}

class _ProfileEditViewState extends State<ProfileEditView> {
  // Per-State so two mounted views never share one GlobalKey.
  final _formKey = GlobalKey<FormState>();
  final pev = ProfileEditViewModel.instance;

  @override
  void initState() {
    super.initState();
    pev.formKey = _formKey;
    // Seed the controllers once per mount; doing this in build() wiped user
    // input on every rebuild.
    pev.initEdit(context);
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(
        leadingWidth: 80,
        leading: const NavigationPopIcon(),
        title: Text(LocalKeys.editProfile),
      ),
      body: ValueListenableBuilder(
        valueListenable: pev.loading,
        builder: (context, loading, child) {
          if (loading) return const Center(child: CustomPreloader());

          return SingleChildScrollView(
            padding: const EdgeInsets.fromLTRB(20, 20, 20, 20),
            child: Form(
              key: _formKey,
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  _SectionHeader(LocalKeys.basicInformation),
                  const Center(child: ProfileInfoAvatar()),
                  16.toHeight,
                  FieldWithLabel(
                    label: LocalKeys.fullName,
                    hintText: LocalKeys.enterFullName,
                    controller: pev.fullNameController,
                    textInputAction: TextInputAction.next,
                    autofillHints: const [AutofillHints.name],
                    validator: pev.validateFullName,
                  ),
                  FieldWithLabel(
                    label: LocalKeys.tagline,
                    hintText: LocalKeys.enterTagline,
                    controller: pev.taglineController,
                    textInputAction: TextInputAction.next,
                    maxLength: 100,
                    validator: pev.validateTagline,
                  ),
                  FieldWithLabel(
                    label: LocalKeys.bio,
                    hintText: LocalKeys.enterBio,
                    controller: pev.bioController,
                    keyboardType: TextInputType.multiline,
                    textInputAction: TextInputAction.newline,
                    minLines: 4,
                    maxLines: 8,
                    validator: pev.validateBio,
                  ),
                  const _ReadOnlyContact(),
                  16.toHeight,
                  _SectionHeader(LocalKeys.professionalDetails),
                  ValueListenableBuilder(
                    valueListenable: pev.categories,
                    builder: (context, options, child) => ValueListenableBuilder(
                      valueListenable: pev.selectedCategory,
                      builder: (context, selected, child) => CategoryDropdownField(
                        options: options,
                        selected: selected,
                        onChanged: (option) => pev.selectedCategory.value = option,
                      ),
                    ),
                  ),
                  FieldLabel(label: LocalKeys.experienceLevel),
                  ValueListenableBuilder(
                    valueListenable: pev.experienceLevel,
                    builder: (context, level, child) => _ExperienceDropdown(
                      value: level,
                      onChanged: (value) => pev.experienceLevel.value = value,
                    ),
                  ),
                  ValueListenableBuilder(
                    valueListenable: pev.availability,
                    builder: (context, available, child) => _AvailabilitySwitch(
                      value: available,
                      onChanged: (value) => pev.availability.value = value,
                    ),
                  ),
                  ValueListenableBuilder(
                    valueListenable: pev.skills,
                    builder: (context, skills, child) => SkillTagsInput(
                      skills: skills,
                      controller: pev.skillController,
                      onAdd: pev.addSkill,
                      onRemove: pev.removeSkill,
                      label: LocalKeys.skills,
                      hintText: LocalKeys.enterSkill,
                    ),
                  ),
                  16.toHeight,
                  _SectionHeader(LocalKeys.servicePackages),
                  PackageTierSection(
                    title: LocalKeys.starter,
                    subtitle: LocalKeys.goodEntryPoint,
                    priceController: pev.starterPriceController,
                    daysController: pev.starterDaysController,
                    descriptionController: pev.starterDescriptionController,
                    priceValidator: pev.validatePrice,
                    daysValidator: pev.validateDeliveryDays,
                  ),
                  12.toHeight,
                  PackageTierSection(
                    title: LocalKeys.pro,
                    subtitle: LocalKeys.largerInvolvedWork,
                    priceController: pev.proPriceController,
                    daysController: pev.proDaysController,
                    descriptionController: pev.proDescriptionController,
                    priceValidator: pev.validatePrice,
                    daysValidator: pev.validateDeliveryDays,
                  ),
                  16.toHeight,
                  // Past-work rows. They are not part of the Save button --
                  // `UpdateFreelancerProfileRequest` has no portfolio field, so
                  // each row commits immediately through its own endpoint.
                  PortfolioSection(pev: pev),
                ],
              ),
            ),
          );
        },
      ),
      // `SafeArea` lifts the Save button clear of the system navigation bar;
      // the app is edge-to-edge, so without it the lower half of the button
      // sits under the gesture pill / 3-button bar and cannot be tapped.
      bottomNavigationBar: SafeArea(
        top: false,
        child: Container(
          margin: const EdgeInsets.all(20),
          color: Colors.transparent,
          child: ValueListenableBuilder(
            valueListenable: pev.saving,
            builder: (context, saving, child) => CustomButton(
              onPressed: saving ? null : () => pev.updateProfileInfo(context),
              btText: LocalKeys.saveChanges,
              isLoading: saving,
            ),
          ),
        ),
      ),
    );
  }
}

class _SectionHeader extends StatelessWidget {
  final String title;
  const _SectionHeader(this.title);

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.only(bottom: 12),
      child: Text(title, style: context.titleMedium?.bold),
    );
  }
}

/// Email and phone cannot be saved from here.
///
/// Changing the address goes through `PUT /api/auth/me`, which stores a
/// *pending* email and emails an OTP before it takes effect, and the phone needs
/// `POST /api/profile/freelancer/send-phone-otp` plus a verify call. Neither is
/// a field on the freelancer profile, and the old form offered both as editable
/// boxes whose values were silently discarded on save.
class _ReadOnlyContact extends StatelessWidget {
  const _ReadOnlyContact();

  @override
  Widget build(BuildContext context) {
    final pi = context.dProvider;
    return Container(
      width: double.infinity,
      padding: const EdgeInsets.all(12),
      decoration: BoxDecoration(
        borderRadius: BorderRadius.circular(8),
        color: pi.primary05,
        border: Border.all(color: pi.primary20),
      ),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Icon(Icons.lock_outline, size: 18, color: pi.primaryColor),
          10.toWidth,
          Expanded(
            child: Text(
              LocalKeys.contactDetailsLocked,
              style: context.titleSmall?.copyWith(color: pi.black5),
            ),
          ),
        ],
      ),
    );
  }
}

/// Experience level.
///
/// The options are the API's own values, not display labels. The previous list
/// was "junior" / "midLevel" / "senior", none of which the validator accepts, so
/// every save was rejected with a 422 and a profile already set to
/// "intermediate" showed no selection at all.
class _ExperienceDropdown extends StatelessWidget {
  final String? value;
  final ValueChanged<String?> onChanged;

  const _ExperienceDropdown({required this.value, required this.onChanged});

  @override
  Widget build(BuildContext context) {
    const levels = FreelancerProfile.experienceLevels;
    return Container(
      height: 46,
      width: double.infinity,
      margin: const EdgeInsets.symmetric(vertical: 5),
      padding: const EdgeInsets.symmetric(horizontal: 12),
      alignment: Alignment.center,
      decoration: BoxDecoration(
        borderRadius: BorderRadius.circular(8),
        border: Border.all(color: context.dProvider.black8),
      ),
      child: DropdownButtonHideUnderline(
        child: DropdownButton<String>(
          isExpanded: true,
          isDense: true,
          borderRadius: BorderRadius.circular(8),
          dropdownColor: context.dProvider.whiteColor,
          icon: Icon(
            Icons.keyboard_arrow_down_sharp,
            color: context.dProvider.black5,
          ),
          value: levels.contains(value) ? value : null,
          hint: Text(
            LocalKeys.selectExperience,
            style: context.titleSmall
                ?.copyWith(color: context.dProvider.black5, fontSize: 14),
          ),
          style: context.titleSmall?.copyWith(
            color: context.dProvider.black5,
            fontSize: 14,
          ),
          onChanged: onChanged,
          items: [
            for (final level in levels)
              DropdownMenuItem(
                value: level,
                child: Text(level.tr().capitalize),
              ),
          ],
        ),
      ),
    );
  }
}

class _AvailabilitySwitch extends StatelessWidget {
  final bool value;
  final ValueChanged<bool> onChanged;

  const _AvailabilitySwitch({required this.value, required this.onChanged});

  @override
  Widget build(BuildContext context) {
    return SwitchListTile.adaptive(
      contentPadding: EdgeInsets.zero,
      value: value,
      onChanged: onChanged,
      title: Text(
        LocalKeys.availableForWork,
        style: context.titleMedium,
      ),
    );
  }
}
