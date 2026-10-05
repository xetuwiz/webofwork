import 'package:flutter/material.dart';
import 'package:provider/provider.dart';
import 'package:webofwork/helper/extension/context_extension.dart';
import 'package:webofwork/helper/extension/int_extension.dart';
import 'package:webofwork/services/project_list_service.dart';
import 'package:webofwork/view_models/home_drawer_view_model/home_drawer_view_model.dart';

/// One chip per applied find-jobs filter, rendered under the search field.
///
/// Each chip's `X` clears exactly that filter (and its matching drawer control,
/// so "Apply" later does not resurrect it); "Clear all" wipes every filter at
/// once. Without this row nothing on the home screen said *which* filters were
/// live: the drawer showed the applied values only while it was open.
class ActiveFilterChips extends StatelessWidget {
  const ActiveFilterChips({super.key});

  @override
  Widget build(BuildContext context) {
    return Consumer<ProjectListService>(
      builder: (context, jl, child) {
        final chips = <Widget>[];

        if (jl.category != null) {
          chips.add(_FilterChip(
            label: jl.category!.name ?? '',
            onDeleted: () {
              final hdm = HomeDrawerViewModel.instance;
              hdm.selectedCategory.value = null;
              jl.clearCategoryFilter();
            },
          ));
        }
        if (jl.skill != null && jl.skill!.isNotEmpty) {
          chips.add(_FilterChip(
            label: jl.skill!,
            onDeleted: () {
              HomeDrawerViewModel.instance.selectedSkill.value = null;
              jl.clearSkill();
            },
          ));
        }
        final minP = jl.minPrice;
        final maxP = jl.maxPrice;
        if ((minP != null && minP.trim().isNotEmpty) ||
            (maxP != null && maxP.trim().isNotEmpty)) {
          final label = [
            if (minP != null && minP.trim().isNotEmpty) '\$${minP.trim()}',
            if (maxP != null && maxP.trim().isNotEmpty) '\$${maxP.trim()}',
          ].join(' - ');
          chips.add(_FilterChip(
            label: label,
            onDeleted: () {
              final hdm = HomeDrawerViewModel.instance;
              hdm.minPriceController.clear();
              hdm.maxPriceController.clear();
              jl.clearPriceRange();
            },
          ));
        }
        if (jl.length != null) {
          chips.add(_FilterChip(
            label: jl.length!,
            onDeleted: () {
              HomeDrawerViewModel.instance.selectedLengths.value = null;
              jl.clearLengthFilter();
            },
          ));
        }
        if (jl.searchText.trim().isNotEmpty) {
          chips.add(_FilterChip(
            label: 'Search: ${jl.searchText.trim()}',
            onDeleted: () {
              HomeDrawerViewModel.instance.searchController.clear();
              jl.clearSearchFilter();
            },
          ));
        }

        if (chips.isEmpty) return const SizedBox.shrink();

        return Padding(
          padding: const EdgeInsets.fromLTRB(20, 12, 20, 0),
          child: Row(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Expanded(
                child: Wrap(
                  spacing: 8,
                  runSpacing: 8,
                  children: chips,
                ),
              ),
              if (jl.hasActiveFilters || jl.searchText.trim().isNotEmpty) ...[
                8.toWidth,
                GestureDetector(
                  onTap: () {
                    HomeDrawerViewModel.instance.resetFilters(context);
                  },
                  child: Padding(
                    padding: const EdgeInsets.only(top: 6),
                    child: Text(
                      'Clear all',
                      style: context.titleSmall?.copyWith(
                        color: context.dProvider.primaryColor,
                        fontWeight: FontWeight.w600,
                      ),
                    ),
                  ),
                ),
              ],
            ],
          ),
        );
      },
    );
  }
}

class _FilterChip extends StatelessWidget {
  final String label;
  final VoidCallback onDeleted;

  const _FilterChip({required this.label, required this.onDeleted});

  @override
  Widget build(BuildContext context) {
    return InputChip(
      label: Text(label),
      onDeleted: onDeleted,
      deleteIcon: const Icon(Icons.close, size: 15),
      backgroundColor: context.dProvider.primary10,
      labelStyle: context.titleSmall?.copyWith(
        color: context.dProvider.primaryColor,
        fontSize: 12,
      ),
      side: BorderSide(color: context.dProvider.primary20),
      visualDensity: VisualDensity.compact,
      materialTapTargetSize: MaterialTapTargetSize.shrinkWrap,
      padding: const EdgeInsets.symmetric(horizontal: 6),
    );
  }
}