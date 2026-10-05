import 'package:flutter/material.dart';
import 'package:webofwork/helper/extension/context_extension.dart';
import 'package:webofwork/utils/components/category_dropdown.dart';

import '../../../view_models/home_drawer_view_model/home_drawer_view_model.dart';

/// The only category control in the drawer.
///
/// The template also rendered a `SubcategoryDropdown` underneath. WebOfWork has
/// no sub-category concept server-side -- there is no `SubCategory` entity, route
/// or DTO -- and the job-list endpoint does not bind one, so the picker could only
/// ever have offered an empty list. Only the category is a real filter here.
class FilterCategories extends StatelessWidget {
  const FilterCategories({
    super.key,
  });

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
          CategoryDropdown(
            catNotifier: hdm.selectedCategory,
          ),
        ],
      ),
    );
  }
}
