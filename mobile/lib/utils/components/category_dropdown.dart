import 'dart:async';

import 'package:flutter/material.dart';
import 'package:provider/provider.dart';
import 'package:webofwork/services/category_dropdown_service.dart';

import '../../helper/extension/context_extension.dart';
import '../../helper/extension/int_extension.dart';
// `toSVGSized` / `toSVG` live on String.
import '../../helper/extension/string_extension.dart';
import '../../helper/local_keys.g.dart';
import '../../helper/svg_assets.dart';
import '../../models/category_model.dart';
import '../../utils/components/custom_preloader.dart';
import '../../utils/components/field_label.dart';
import 'empty_spacer_helper.dart';

/// Category picker used by the job-filter drawer.
///
/// Converted from a `StatelessWidget` that held mutable state on itself, which the
/// analyzer flagged (`must_be_immutable`) and which was wrong in three ways:
///
///  * `final ScrollController controller` was allocated per widget instance and
///    **never disposed**, leaking a listener registration on every rebuild that
///    produced a new instance.
///  * `controller.addListener(...)` was called from inside the
///    `showModalBottomSheet` builder, so opening the sheet N times attached N
///    listeners, each of which ran `tryLoadingMore` on every scroll event.
///  * `Timer? scheduleTimeout` was a field on the widget too, so a pending
///    one-second debounce had no owner to cancel it.
///
/// All three now live in [State]: the controller is created in `initState` and
/// disposed in `dispose`, and no listener is registered at all.
///
/// The search is client-side against a list loaded once
/// ([CategoryDropdownService]), because the `?category=` parameter the template
/// used does not exist on this API and was being dropped silently.
class CategoryDropdown extends StatefulWidget {
  final String? textFieldHint;
  final dynamic onChanged;
  final dynamic textStyle;
  final dynamic isRequired;
  final ValueNotifier<Category?> catNotifier;

  const CategoryDropdown({
    super.key,
    this.textFieldHint,
    this.onChanged,
    this.textStyle,
    this.isRequired,
    required this.catNotifier,
  });

  @override
  State<CategoryDropdown> createState() => _CategoryDropdownState();
}

class _CategoryDropdownState extends State<CategoryDropdown> {
  /// Only the picker sheet's list scrolls with this; the collapsed field is static.
  final _sheetScrollController = ScrollController();
  final _searchController = TextEditingController();

  @override
  void dispose() {
    _sheetScrollController.dispose();
    _searchController.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return ValueListenableBuilder(
      valueListenable: widget.catNotifier,
      builder: (context, selectedValue, child) => Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          FieldLabel(
            label: LocalKeys.category,
            isRequired: widget.isRequired ?? false,
          ),
          InkWell(
            onTap: () => _openSheet(context, selectedValue),
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
                    flex: 1,
                    child: Text(
                      selectedValue?.name ?? LocalKeys.selectCategory,
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
          EmptySpaceHelper.emptyHeight(12),
        ],
      ),
    );
  }

  Future<void> _openSheet(BuildContext context, Category? selectedValue) async {
    final service = context.read<CategoryDropdownService>();
    // Warm the cache. The template cleared and refetched on every tap; this only
    // hits the network the first time, or after an explicit `reset()`.
    unawaited(service.getCategories());

    // The previous sheet's search text must not leak into the new one.
    _searchController.clear();
    service.search('');

    await showModalBottomSheet(
      context: context,
      backgroundColor: Colors.transparent,
      isScrollControlled: true,
      builder: (sheetContext) => _buildSheet(sheetContext, selectedValue),
    );
  }

  Widget _buildSheet(BuildContext context, Category? selectedValue) {
    return Container(
      decoration: BoxDecoration(
        borderRadius: BorderRadius.circular(20),
        color: context.dProvider.whiteColor,
      ),
      constraints: BoxConstraints(
        maxHeight: context.height / 2 + (MediaQuery.of(context).viewInsets.bottom / 2),
      ),
      child: Consumer<CategoryDropdownService>(
        builder: (context, service, child) {
          final rows = service.categories;
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
                    hintText: widget.textFieldHint ?? LocalKeys.searchCategory,
                    prefixIcon: Padding(
                      padding: const EdgeInsets.symmetric(horizontal: 12),
                      child: SvgAssets.search.toSVG,
                    ),
                  ),
                  // Client-side and synchronous, so there is nothing to debounce
                  // and no timer that can outlive the sheet.
                  onChanged: service.search,
                ),
              ),
              Expanded(
                child: _buildList(context, service, rows, selectedValue),
              ),
            ],
          );
        },
      ),
    );
  }

  Widget _buildList(
    BuildContext context,
    CategoryDropdownService service,
    List<Category> rows,
    Category? selectedValue,
  ) {
    if (service.loading && rows.isEmpty) {
      return const Center(child: CustomPreloader());
    }
    if (rows.isEmpty) {
      return Center(
        child: Text(LocalKeys.noResultFound, style: widget.textStyle),
      );
    }
    return ListView.separated(
      controller: _sheetScrollController,
      padding: const EdgeInsets.only(right: 20, left: 20, bottom: 20),
      itemCount: rows.length,
      separatorBuilder: (context, index) => const SizedBox(
        height: 8,
        child: Center(child: Divider()),
      ),
      itemBuilder: (context, index) {
        final element = rows[index];
        return InkWell(
          onTap: () {
            Navigator.pop(context);
            // `Category` has value equality now, so re-picking the current
            // selection is a genuine no-op instead of firing a refetch.
            if (element == selectedValue) return;
            widget.catNotifier.value = element;
            widget.onChanged?.call(element);
          },
          child: Padding(
            padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 14),
            child: Text(element.name ?? '', style: widget.textStyle),
          ),
        );
      },
    );
  }
}
