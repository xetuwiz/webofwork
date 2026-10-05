import 'dart:async';

import 'package:flutter/material.dart';
import 'package:provider/provider.dart';
import 'package:webofwork/models/skill_model.dart';
import 'package:webofwork/services/skill_dropdown_service.dart';

import '../../helper/extension/context_extension.dart';
import '../../helper/extension/int_extension.dart';
// `toSVGSized` / `toSVG` live on String.
import '../../helper/extension/string_extension.dart';
import '../../helper/local_keys.g.dart';
import '../../helper/svg_assets.dart';
import '../../utils/components/custom_preloader.dart';
import '../../utils/components/field_label.dart';

/// Skill picker for the job-filter drawer.
///
/// Mirrors [CategoryDropdown]'s searchable bottom-sheet pattern: the whole
/// (small, public) skills set is fetched once via [SkillDropdownService] and
/// filtered in memory, against a search field that needs no debounce timer.
///
/// The selected value is the skill **name** -- `ProjectsController
/// FreelancerList` matches `x.Skill.Name.ToLower() == skill.ToLower()`, so an id
/// (what the web client sends) would never match a row.
class SkillDropdown extends StatefulWidget {
  /// What to call the collapsed field: `LocalKeys.selectSkill` in the drawer.
  final String hintText;

  /// Living selection. The drawer feeds it `HomeDrawerViewModel.selectedSkill`.
  final ValueNotifier<String?> notifier;

  /// Called with the picked name (after the notifier is updated).
  final ValueChanged<String>? onChanged;

  const SkillDropdown({
    super.key,
    required this.hintText,
    required this.notifier,
    this.onChanged,
  });

  @override
  State<SkillDropdown> createState() => _SkillDropdownState();
}

class _SkillDropdownState extends State<SkillDropdown> {
  final _searchController = TextEditingController();

  @override
  void dispose() {
    _searchController.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return ValueListenableBuilder(
      valueListenable: widget.notifier,
      builder: (context, selected, child) => Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          FieldLabel(label: LocalKeys.skill),
          InkWell(
            onTap: _openSheet,
            child: Container(
              height: 50,
              decoration: BoxDecoration(
                borderRadius: BorderRadius.circular(8),
                border: Border.all(color: context.dProvider.black7, width: 1),
              ),
              child: Row(
                children: [
                  8.toWidth,
                  Expanded(
                    child: Text(
                      selected ?? widget.hintText,
                      maxLines: 1,
                      overflow: TextOverflow.ellipsis,
                      style: context.titleSmall?.copyWith(
                        color: context.dProvider.black6,
                        fontWeight: FontWeight.w600,
                      ),
                    ),
                  ),
                  Padding(
                    padding: const EdgeInsets.symmetric(horizontal: 12),
                    child: SvgAssets.arrowDown.toSVGSized(24.0),
                  ),
                ],
              ),
            ),
          ),
          12.toHeight,
        ],
      ),
    );
  }

  Future<void> _openSheet() async {
    final service = context.read<SkillDropdownService>();
    unawaited(service.getSkills());

    // The previous sheet's search text must not leak into the new one.
    _searchController.clear();
    service.search('');

    await showModalBottomSheet(
      context: context,
      backgroundColor: Colors.transparent,
      isScrollControlled: true,
      builder: (sheetContext) => _buildSheet(sheetContext),
    );
  }

  Widget _buildSheet(BuildContext context) {
    return Container(
      decoration: BoxDecoration(
        borderRadius: BorderRadius.circular(20),
        color: context.dProvider.whiteColor,
      ),
      constraints: BoxConstraints(
        maxHeight:
            context.height / 2 + (MediaQuery.of(context).viewInsets.bottom / 2),
      ),
      child: Consumer<SkillDropdownService>(
        builder: (context, service, child) {
          final rows = service.skills;
          return Column(
            children: [
              Align(
                alignment: Alignment.center,
                child: Container(
                  height: 4,
                  width: 48,
                  margin: const EdgeInsets.all(8),
                  decoration: BoxDecoration(
                    borderRadius: BorderRadius.circular(12),
                    color: context.dProvider.black7,
                  ),
                ),
              ),
              Padding(
                padding: const EdgeInsets.symmetric(horizontal: 20, vertical: 10),
                child: TextField(
                  controller: _searchController,
                  decoration: InputDecoration(
                    hintText: LocalKeys.searchCategory,
                    prefixIcon: Padding(
                      padding: const EdgeInsets.symmetric(horizontal: 12),
                      child: SvgAssets.search.toSVG,
                    ),
                  ),
                  onChanged: service.search,
                ),
              ),
              Expanded(child: _buildList(context, service, rows)),
            ],
          );
        },
      ),
    );
  }

  Widget _buildList(
    BuildContext context,
    SkillDropdownService service,
    List<Skill> rows,
  ) {
    if (service.loading && rows.isEmpty) {
      return const Center(child: CustomPreloader());
    }
    if (rows.isEmpty) {
      return Center(child: Text(LocalKeys.noResultFound));
    }
    return ListView.separated(
      padding: const EdgeInsets.only(right: 20, left: 20, bottom: 20),
      itemCount: rows.length,
      separatorBuilder: (context, index) =>
          const SizedBox(height: 8, child: Center(child: Divider())),
      itemBuilder: (context, index) {
        final name = rows[index].name;
        return InkWell(
          onTap: () {
            Navigator.pop(context);
            if (widget.notifier.value == name) return;
            widget.notifier.value = name;
            widget.onChanged?.call(name);
          },
          child: Padding(
            padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 14),
            child: Text(name),
          ),
        );
      },
    );
  }
}