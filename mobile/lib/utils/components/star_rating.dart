import 'package:flutter/material.dart';

/// Five-star input/display used by the review sheet (tappable) and the review
/// cards (read-only).
///
/// Matches the reference's star picker: 1–5, defaulting to 5, with empty stars
/// shown in the muted grey. Exporting it as one widget keeps the sheet and the
/// two review cards from drifting apart in size and colour.
class StarRating extends StatelessWidget {
  const StarRating({
    super.key,
    required this.rating,
    this.size = 24,
    this.onChanged,
  });

  /// 0 (no rating yet) renders all stars empty. Must be 0–5.
  final int rating;
  final double size;

  /// When non-null the stars are tappable and [rating] is re-derived on each
  /// tap. When null the widget is display-only.
  final ValueChanged<int>? onChanged;

  @override
  Widget build(BuildContext context) {
    return Row(
      mainAxisSize: MainAxisSize.min,
      children: List.generate(
        5,
        (i) {
          final filled = i < rating;
          return InkWell(
            onTap: onChanged == null ? null : () => onChanged!(i + 1),
            borderRadius: BorderRadius.circular(4),
            child: Padding(
              padding: const EdgeInsets.symmetric(horizontal: 1),
              child: Icon(
                filled ? Icons.star_rounded : Icons.star_border_rounded,
                size: size,
                color: filled ? const Color(0xFFF59E0B) : Colors.grey.shade400,
              ),
            ),
          );
        },
      ),
    );
  }
}