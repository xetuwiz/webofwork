import 'package:flutter/material.dart';
import 'package:webofwork/helper/extension/context_extension.dart';
import 'package:webofwork/services/invitation_service.dart';

/// Why the platform thinks this freelancer is a fit.
///
/// ## This is the point of the feature, not decoration
///
/// An invitation that arrives from an AI-matched shortlist and one that arrives
/// because a client tapped a name look identical to the freelancer. That is the
/// information they need in order to decide whether to spend an hour writing a
/// proposal, and without it the whole matching system is invisible to the person
/// it is supposed to be helping.
///
/// So this is a disclosure the freelancer controls rather than a panel that is
/// always open. The summary line is always visible -- it is one tap to read and
/// costs nothing -- and the supporting detail expands, because a freelancer
/// scanning six pending invitations does not want six walls of text.
///
/// ## It never appears without a reason
///
/// [MatchContext.hasContent] gates the whole card. A match the model explained
/// nothing about shows nothing, rather than an empty panel that implies the
/// platform checked and had nothing to say. Silence and "we looked and found
/// nothing" are different claims.
class MatchReasonCard extends StatelessWidget {
  final MatchContext match;
  final bool initiallyExpanded;

  const MatchReasonCard({
    super.key,
    required this.match,
    this.initiallyExpanded = false,
  });

  @override
  Widget build(BuildContext context) {
    if (!match.hasContent) return const SizedBox.shrink();

    final hasDetail = match.strengths.isNotEmpty || match.gaps.isNotEmpty;

    return Container(
      margin: const EdgeInsets.only(top: 12),
      decoration: BoxDecoration(
        // Tinted rather than bordered. It sits inside a card that already has a
        // border, and a second border reads as a card inside a card.
        color: context.dProvider.primary05,
        borderRadius: BorderRadius.circular(8),
        border: Border.all(
          color: context.dProvider.primary20,
        ),
      ),
      child: Theme(
        // Removes the divider lines ExpansionTile draws by default, which at
        // this size are visual noise around a two-line summary.
        data: Theme.of(context).copyWith(dividerColor: Colors.transparent),
        child: ExpansionTile(
          initiallyExpanded: initiallyExpanded,
          tilePadding: const EdgeInsets.symmetric(horizontal: 12, vertical: 2),
          childrenPadding: const EdgeInsets.fromLTRB(12, 0, 12, 12),
          shape: const Border(),
          collapsedShape: const Border(),
          // The score, once, as a leading badge. It is the headline number for
          // anyone who wants only that.
          leading: _ScoreBadge(score: match.scoreAsPercent),
          title: Text(
            'Why you were matched',
            style: context.titleSmall?.copyWith(fontWeight: FontWeight.bold),
          ),
          subtitle: match.reason == null || match.reason!.trim().isEmpty
              ? null
              : Padding(
                  padding: const EdgeInsets.only(top: 2),
                  child: Text(
                    match.reason!,
                    style: context.titleSmall
                        ?.copyWith(color: context.dProvider.black5),
                  ),
                ),
          children: hasDetail
              ? [
                  if (match.strengths.isNotEmpty)
                    _DetailList(
                      title: 'What matched',
                      icon: Icons.check_circle_outline,
                      items: match.strengths,
                      color: context.dProvider.greenColor,
                    ),
                  if (match.gaps.isNotEmpty)
                    _DetailList(
                      title: 'What you do not have',
                      icon: Icons.info_outline,
                      items: match.gaps,
                      color: context.dProvider.black5,
                    ),
                ]
              : const [],
        ),
      ),
    );
  }
}

class _ScoreBadge extends StatelessWidget {
  final String score;

  const _ScoreBadge({required this.score});

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 4),
      decoration: BoxDecoration(
        color: context.dProvider.primaryColor,
        borderRadius: BorderRadius.circular(6),
      ),
      child: Text(
        score,
        style: context.titleSmall?.copyWith(
          color: context.dProvider.whiteColor,
          fontWeight: FontWeight.bold,
        ),
      ),
    );
  }
}

class _DetailList extends StatelessWidget {
  final String title;
  final IconData icon;
  final List<String> items;
  final Color color;

  const _DetailList({
    required this.title,
    required this.icon,
    required this.items,
    required this.color,
  });

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.only(top: 10),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              Icon(icon, size: 14, color: color),
              const SizedBox(width: 6),
              Text(
                title,
                style: context.titleSmall?.copyWith(
                  fontWeight: FontWeight.bold,
                  color: color,
                ),
              ),
            ],
          ),
          const SizedBox(height: 6),
          // Wrapped, not a Column: these come from a model and their lengths are
          // not known in advance. A fixed-height list would overflow on a narrow
          // phone with a long skill name, which is the most likely place this
          // ever renders.
          Wrap(
            spacing: 6,
            runSpacing: 6,
            children: [
              for (final item in items)
                Container(
                  padding:
                      const EdgeInsets.symmetric(horizontal: 8, vertical: 3),
                  decoration: BoxDecoration(
                    color: context.dProvider.whiteColor,
                    borderRadius: BorderRadius.circular(4),
                    border: Border.all(color: context.dProvider.black8),
                  ),
                  child: Text(item, style: context.titleSmall),
                ),
            ],
          ),
        ],
      ),
    );
  }
}
