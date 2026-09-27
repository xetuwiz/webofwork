import 'package:flutter/material.dart';
import 'package:webofwork/helper/extension/context_extension.dart';
import 'package:webofwork/helper/extension/int_extension.dart';

/// A labelled switch row. Used by both the channel group and the per-type group
/// so the two sections cannot drift apart visually.
class PreferenceSwitchTile extends StatelessWidget {
  final String title;
  final String? subtitle;
  final bool value;

  /// Null disables the row and greys it -- used when push is unavailable on the
  /// device, where flipping the switch could not possibly do anything.
  final VoidCallback? onChanged;
  final Widget? leading;

  const PreferenceSwitchTile({
    super.key,
    required this.title,
    required this.value,
    this.subtitle,
    this.onChanged,
    this.leading,
  });

  @override
  Widget build(BuildContext context) {
    // Both optional fields are hoisted into locals so their null-check is promoted
    // once, up front. Inside a `(_)` callback the promotion of `onChanged` does not
    // hold, so `onChanged()` fails to compile and `onChanged!()` is only papered
    // over by a lint that then breaks the build. Same reasoning for `subtitle`
    // and `leading`.
    final handler = onChanged;
    final enabled = handler != null;
    final note = subtitle;
    final hasNote = note != null && note.isNotEmpty;
    final badge = leading;
    return Padding(
      padding: const EdgeInsets.symmetric(vertical: 4),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          if (badge != null) ...[badge, 12.toWidth],
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  title,
                  style: context.titleSmall?.bold6.copyWith(
                    // A disabled row still has to be readable, so this is the
                    // muted token rather than a reduced opacity that would drop
                    // it below the contrast floor.
                    color: enabled ? null : context.dProvider.black5,
                  ),
                ),
                if (hasNote) ...[
                  2.toHeight,
                  Text(
                    // No `!`: `hasNote` already promotes `note` for the whole
                    // list literal, so the assertion is redundant.
                    note,
                    style: context.titleSmall
                        ?.copyWith(color: context.dProvider.black5, height: 1.4),
                  ),
                ],
              ],
            ),
          ),
          12.toWidth,
          Switch(value: value, onChanged: handler == null ? null : (_) => handler()),
        ],
      ),
    );
  }
}

/// `PreferenceSectionHeader` was removed in favour of the shared
/// `AppSectionHeader` (utils/components/app_card.dart). Two section headers with
/// different padding and alignment is precisely the drift this file's siblings
/// exist to stop.
///
/// Inline advisory. An `info` strip rather than a dialog: it explains a
/// permanent state (push blocked by the OS) that the user has to fix elsewhere,
/// and it should stay visible while they are deciding.
class PreferenceNotice extends StatelessWidget {
  final String message;
  final IconData icon;
  final Color? background;
  final Color? foreground;
  final Widget? trailing;

  const PreferenceNotice({
    super.key,
    required this.message,
    this.icon = Icons.info_outline,
    this.background,
    this.foreground,
    this.trailing,
  });

  @override
  Widget build(BuildContext context) {
    final bg = background ?? context.dProvider.primary05;
    final fg = foreground ?? context.dProvider.primaryColor;
    return Container(
      width: double.infinity,
      padding: const EdgeInsets.all(14),
      decoration: BoxDecoration(
        color: bg,
        borderRadius: BorderRadius.circular(12),
        border: Border.all(color: fg.withValues(alpha: 0.2)),
      ),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Icon(icon, size: 18, color: fg),
          10.toWidth,
          Expanded(
            child: Text(
              message,
              style: context.titleSmall?.copyWith(color: fg, height: 1.4),
            ),
          ),
          if (trailing != null) ...[8.toWidth, trailing!],
        ],
      ),
    );
  }
}
