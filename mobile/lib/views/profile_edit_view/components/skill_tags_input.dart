import 'package:flutter/material.dart';
import 'package:webofwork/helper/extension/context_extension.dart';
import 'package:webofwork/helper/extension/int_extension.dart';
import 'package:webofwork/utils/components/field_label.dart';

/// Free-form skill tags, matching the web client's profile editor: type a name,
/// press enter, tap the x to remove.
///
/// The API stores skills by name, matching an existing `Skills` row where it can
/// and falling back to a custom name otherwise, so this deliberately has no
/// fixed vocabulary to choose from.
class SkillTagsInput extends StatefulWidget {
  final List<String> skills;
  final TextEditingController controller;
  final ValueChanged<String> onAdd;
  final ValueChanged<String> onRemove;
  final String label;
  final String hintText;

  const SkillTagsInput({
    super.key,
    required this.skills,
    required this.controller,
    required this.onAdd,
    required this.onRemove,
    required this.label,
    required this.hintText,
  });

  @override
  State<SkillTagsInput> createState() => _SkillTagsInputState();
}

class _SkillTagsInputState extends State<SkillTagsInput> {
  @override
  Widget build(BuildContext context) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        FieldLabel(label: widget.label),
        if (widget.skills.isNotEmpty) ...[
          Wrap(
            spacing: 8,
            runSpacing: 8,
            children: [
              for (final skill in widget.skills)
                InputChip(
                  label: Text(skill),
                  onDeleted: () => widget.onRemove(skill),
                  deleteIcon: const Icon(Icons.close, size: 16),
                  backgroundColor: context.dProvider.primary10,
                  labelStyle: context.titleSmall?.copyWith(
                    color: context.dProvider.primaryColor,
                    fontSize: 13,
                  ),
                  side: BorderSide(color: context.dProvider.primary20),
                  visualDensity: VisualDensity.compact,
                ),
            ],
          ),
          8.toHeight,
        ],
        TextField(
          controller: widget.controller,
          textInputAction: TextInputAction.done,
          // Comma is the other conventional separator and costs nothing.
          onSubmitted: widget.onAdd,
          decoration: InputDecoration(
            hintText: widget.hintText,
            prefixIcon: const Icon(Icons.add, size: 20),
            suffixIcon: IconButton(
              tooltip: 'Add',
              icon: const Icon(Icons.arrow_forward, size: 20),
              onPressed: () => widget.onAdd(widget.controller.text),
            ),
          ),
        ),
        16.toHeight,
      ],
    );
  }
}
