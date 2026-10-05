import 'package:flutter/material.dart';
import 'package:webofwork/helper/extension/context_extension.dart';
import 'package:webofwork/helper/local_keys.g.dart';
import 'package:webofwork/utils/components/skill_dropdown.dart';

import '../../../view_models/home_drawer_view_model/home_drawer_view_model.dart';

/// The skill filter in the find-jobs drawer.
///
/// `ProjectsController.FreelancerList` binds `?skill=` and matches skill
/// **names**, so the picker stores the name. This is the filter whose web
/// counterpart was dead: React sent `skill.id`, the server compared it to
/// `Name.ToLower()`, and no id ever equals a name -- so the dropdown looked
/// live and filtered nothing.
class FilterSkills extends StatelessWidget {
  const FilterSkills({super.key});

  @override
  Widget build(BuildContext context) {
    final hdm = HomeDrawerViewModel.instance;
    return Container(
      padding: const EdgeInsets.all(16),
      decoration: BoxDecoration(
        borderRadius: BorderRadius.circular(8),
        color: context.dProvider.whiteColor,
      ),
      child: Column(
        children: [
          SkillDropdown(
            hintText: LocalKeys.selectSkill,
            notifier: hdm.selectedSkill,
          ),
        ],
      ),
    );
  }
}