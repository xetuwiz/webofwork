import 'package:flutter/material.dart';
import 'package:webofwork/helper/extension/context_extension.dart';
import 'package:webofwork/helper/extension/widget_extension.dart';
import 'package:webofwork/helper/local_keys.g.dart';
import 'package:webofwork/services/profile_details_service.dart';

import 'skill_chip.dart';

/// Read-only skill chips.
///
/// `skills` arrives as a JSON **array** of names (the backend joins
/// `FreelancerSkills` to `Skills` and falls back to `CustomName`), so it is
/// rendered as-is. The previous version did
/// `(skills ?? "").split(", ").toList()` on a field typed `String?` that the
/// API fills with a list -- a runtime cast error on every successful load.
class ProfileDetailsSkills extends StatelessWidget {
  final ProfileDetailsService pd;
  const ProfileDetailsSkills({super.key, required this.pd});

  @override
  Widget build(BuildContext context) {
    final skills = pd.profileDetails.skills;
    if (skills.isEmpty) return const SizedBox();
    return Container(
      margin: const EdgeInsets.only(top: 20),
      padding: const EdgeInsets.symmetric(vertical: 20),
      decoration: BoxDecoration(
        borderRadius: BorderRadius.circular(8),
        color: context.dProvider.whiteColor,
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(LocalKeys.skills, style: context.titleMedium?.bold6).hp20,
          Divider(
            color: context.dProvider.black8,
            thickness: 2,
            height: 36,
          ),
          Wrap(
            spacing: 12,
            runSpacing: 12,
            children: skills.map((e) => SkillChip(skill: e)).toList(),
          ).hp20,
        ],
      ),
    );
  }
}
