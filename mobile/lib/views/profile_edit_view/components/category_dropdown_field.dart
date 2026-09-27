import 'package:flutter/material.dart';
import 'package:webofwork/helper/extension/context_extension.dart';
import 'package:webofwork/helper/extension/int_extension.dart';
import 'package:webofwork/helper/local_keys.g.dart';
import 'package:webofwork/models/freelancer_profile_model.dart';
import 'package:webofwork/utils/components/field_label.dart';

/// Category picker for the profile editor.
///
/// Keyed on the category **id** rather than its name, so two categories that
/// happen to share a name cannot be confused -- the id is also what the API
/// stores, and it stays valid when a category is renamed later.
class CategoryDropdownField extends StatelessWidget {
  final List<ProfileCategoryOption> options;
  final ProfileCategoryOption? selected;
  final ValueChanged<ProfileCategoryOption?> onChanged;

  const CategoryDropdownField({
    super.key,
    required this.options,
    required this.selected,
    required this.onChanged,
  });

  @override
  Widget build(BuildContext context) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        FieldLabel(label: LocalKeys.category),
        Container(
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
            child: DropdownButton<int>(
              isExpanded: true,
              isDense: true,
              borderRadius: BorderRadius.circular(8),
              dropdownColor: context.dProvider.whiteColor,
              icon: Icon(
                Icons.keyboard_arrow_down_sharp,
                color: context.dProvider.black5,
              ),
              // Null clears the selection, which sends no category_id and
              // therefore leaves the stored category untouched server-side.
              value: selected?.id,
              hint: Text(
                options.isEmpty ? 'No categories available' : LocalKeys.selectCategory,
                style: context.titleSmall
                    ?.copyWith(color: context.dProvider.black5, fontSize: 14),
              ),
              style: context.titleSmall?.copyWith(
                color: context.dProvider.black5,
                fontSize: 14,
              ),
              onChanged: (id) {
                if (id == null) {
                  onChanged(null);
                  return;
                }
                for (final option in options) {
                  if (option.id == id) {
                    onChanged(option);
                    return;
                  }
                }
              },
              items: [
                for (final option in options)
                  DropdownMenuItem<int>(
                    value: option.id,
                    child: Text(
                      option.name,
                      overflow: TextOverflow.ellipsis,
                    ),
                  ),
              ],
            ),
          ),
        ),
        16.toHeight,
      ],
    );
  }
}
