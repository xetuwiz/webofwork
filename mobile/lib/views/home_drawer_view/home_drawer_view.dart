import 'package:flutter/material.dart';
import 'package:webofwork/helper/extension/context_extension.dart';
import 'package:webofwork/helper/extension/int_extension.dart';
import 'package:webofwork/helper/local_keys.g.dart';
import 'package:webofwork/utils/components/navigation_pop_icon.dart';
import 'package:webofwork/views/home_drawer_view/components/filter_buttons.dart';

import 'components/filter_budget_range.dart';
import 'components/filter_categories.dart';
import 'components/filter_lengths.dart';
import 'components/filter_skills.dart';

/// The job-filter drawer.
///
/// Renders only filters that `ProjectsController.FreelancerList` binds:
/// category, skill, budget, delivery length and (via [FilterButtons]) the search
/// text. The place, experience-level, job-type and rating sections were removed
/// -- this API has no country, experience-level, job-type or rating filter, so
/// they were controls that looked live and changed nothing.
class HomeDrawerView extends StatelessWidget {
  const HomeDrawerView({super.key});

  @override
  Widget build(BuildContext context) {
    return Container(
      child: Column(
        children: [
          AppBar(
            leading: const NavigationPopIcon(),
            title: Text(LocalKeys.filter),
          ),
          Expanded(
            child: ListView(
              padding: const EdgeInsets.all(20),
              children: [
                const FilterCategories(),
                12.toHeight,
                const FilterSkills(),
                12.toHeight,
                const FilterBudgetRange(),
                12.toHeight,
                const FilterLengths(),
              ],
            ),
          ),
          Divider(
            color: context.dProvider.black8,
            thickness: 2,
            height: 2,
          ),
          const FilterButtons(),
        ],
      ),
    );
  }
}
