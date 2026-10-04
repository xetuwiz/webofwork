import 'package:flutter/material.dart';
import 'package:webofwork/helper/extension/context_extension.dart';
import 'package:webofwork/helper/extension/int_extension.dart';

/// The app's radius scale, stated once.
///
/// Derived from a survey of the 51 card-shaped containers in the view layer
/// (`borderRadius` plus a surface colour in the same `BoxDecoration`), not
/// guessed:
///
///   radius  8 -> 35   the dominant card/control radius, and already what
///                     `DefaultThemes` uses for every input and button
///   radius 12 ->  6
///   radius 20 ->  5   all of them pills/badges, not cards
///   radius  6 ->  3
///   radius 10 ->  1   a chip
///   radius  4 ->  1   a progress-bar clip
///
/// So the app already had a coherent uniform-8 system, and the handful of genuine
/// cards that disagreed were drift rather than a second design language. An
/// earlier version of this file asserted the card radius was 20, on the evidence
/// of `job_card.dart` and `fixed_price_job_details_milestone_card.dart` -- but the
/// `circular(20)` in both is a *badge*, not the card. Sampling the wrong element
/// is how a "consistency" change ends up introducing a third radius.
///
/// This class exists to stop the drift returning, and to give the badge radius a
/// name so the next person does not "fix" a pill into a card shape.
class AppRadius {
  const AppRadius._();

  /// Cards and list tiles. One number, on purpose.
  static const double card = 8;

  /// Controls are deliberately the same value as cards, so the app reads as a
  /// single system. Stated separately so a future change to one has to be a
  /// conscious decision rather than an accident.
  static const double control = 8;

  /// Badges and chips, meant to read as fully rounded against the cards they sit
  /// on. Not an error to be normalised away.
  static const double badge = 20;

  /// A true pill, regardless of content width.
  static const double pill = 999;
}

/// The app's spacing scale for stacked content.
///
/// A survey of the list/card layer found card padding at 12, 16, 18 and 20, and
/// the gap between stacked cards at 0, 2, 8 and 12 -- so the same kind of list
/// looked tighter or looser depending on which screen it was on, and two cards
/// in one list could not be made to line up.
///
/// Only the values the view layer actually leans on are named. A full
/// 4/8/12/16/20 scale would be more "complete" and would also be a scale nobody
/// maintains.
class AppSpacing {
  const AppSpacing._();

  /// Vertical gap between two stacked cards in a list. 12 is what the busiest
  /// list (`my_order_card`) already used.
  static const double cardGap = 12;

  /// Horizontal gutter from the screen edge to the content, and the padding
  /// inside a standard card.
  static const double screenGutter = 20;

  /// Gap between a heading and the content it introduces.
  static const double sectionGap = 24;
}

/// The app's one card.
///
/// Screens were hand-rolling `Container(decoration: BoxDecoration(borderRadius:
/// BorderRadius.circular(N), border: Border.all(black8)))`, so two cards sitting
/// on the same screen were visibly different. Everything new goes through this.
class AppCard extends StatelessWidget {
  final Widget child;
  final EdgeInsetsGeometry? padding;
  final VoidCallback? onTap;

  /// `tinted` uses the brand wash for a callout; `flat` is the default surface.
  final bool tinted;
  final Color? background;

  /// Default card padding.
  ///
  /// 20, not 16: `EdgeInsets.all(20)` is what the view layer already used 56
  /// times, versus 19 for 16. Choosing 16 here would have made every new card
  /// 4px tighter than the existing ones -- the exact inconsistency this is meant
  /// to remove. A caller can still pass `padding` for a denser variant.
  static const EdgeInsets defaultPadding = EdgeInsets.all(20);

  const AppCard({
    super.key,
    required this.child,
    this.padding,
    this.onTap,
    this.tinted = false,
    this.background,
  });

  @override
  Widget build(BuildContext context) {
    final decoration = BoxDecoration(
      color: background ??
          (tinted ? context.dProvider.primary05 : context.dProvider.whiteColor),
      borderRadius: BorderRadius.circular(AppRadius.card),
      border: Border.all(
        color: tinted
            ? context.dProvider.primaryColor.withValues(alpha: 0.18)
            : context.dProvider.black8,
      ),
    );

    final content = Container(
      width: double.infinity,
      padding: padding ?? defaultPadding,
      decoration: decoration,
      child: child,
    );

    if (onTap == null) return content;

    // InkWell rather than GestureDetector: the ripple is what makes a card feel
    // tappable, and it needs to be clipped to the same radius as the surface.
    return Material(
      color: Colors.transparent,
      child: InkWell(
        onTap: onTap,
        borderRadius: BorderRadius.circular(AppRadius.card),
        child: content,
      ),
    );
  }
}

/// A heading for a group of rows inside a settings-style screen.
///
/// The app had no section primitive, so every settings-ish screen wrote its own
/// label with a different size and colour.
class AppSectionHeader extends StatelessWidget {
  final String title;
  final String? description;
  final Widget? trailing;

  const AppSectionHeader({
    super.key,
    required this.title,
    this.description,
    this.trailing,
  });

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.only(bottom: 10),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(title, style: context.titleSmall?.bold6),
                if (description != null && description!.isNotEmpty) ...[
                  2.toHeight,
                  Text(
                    description!,
                    style: context.titleSmall
                        ?.copyWith(color: context.dProvider.black5, height: 1.4),
                  ),
                ],
              ],
            ),
          ),
          if (trailing != null) ...[12.toWidth, trailing!],
        ],
      ),
    );
  }
}

/// Deliberately *not* an empty-state widget here.
///
/// `EmptyWidget` (utils/components/empty_widget.dart) already owns that job, with
/// the Lottie animation. A second, plainer empty-state component would be exactly
/// the kind of near-duplicate this file exists to remove -- screens would pick
/// whichever they happened to have imported and the "no results" look would drift
/// apart again. Use `EmptyWidget` for lists, and inline a `Text` for the one-line
/// cases.
