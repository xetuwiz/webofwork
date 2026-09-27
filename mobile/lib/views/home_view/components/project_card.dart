import 'package:flutter/material.dart';
import 'package:webofwork/utils/components/app_card.dart';
import 'package:flutter_widget_from_html/flutter_widget_from_html.dart';
import 'package:intl/intl.dart';
import 'package:webofwork/helper/extension/context_extension.dart';
import 'package:webofwork/helper/extension/datetime_extension.dart';
import 'package:webofwork/helper/extension/int_extension.dart';
import 'package:webofwork/helper/extension/string_extension.dart';
import 'package:webofwork/helper/local_keys.g.dart';
import 'package:webofwork/views/fixed_price_project_details_view/fixed_price_project_details_view.dart';

import '../../../helper/svg_assets.dart';

class ProjectCard extends StatelessWidget {
  final id;
  final title;
  final isFavorite;
  final createDate;
  final expertise;
  final price;
  final priceType;
  final summery;
  final List<String> tags;

  /// Where the job is. **Null when the payload carries no location.**
  ///
  /// This API has no location on a project. The list call site used to pass
  /// `projectCreator?.userCountry?.country ?? "----"` and the dashboard passed a
  /// literal `'Remote'`, so every job card advertised a location that was
  /// invented. Null now omits the row.
  final String? location;

  /// Proposal count. **Null when the endpoint does not send one.**
  ///
  /// `GET /api/projects/freelancer/project-list` has no proposal count, so the
  /// list call site used to render a hard-coded "Proposals: 0" on every card --
  /// stating as fact that nobody had bid. Zero is still rendered when zero is
  /// what the server counted (`GET /api/projects/{id}/details` sends
  /// `proposals_count`); only "unknown" becomes null.
  final int? proposalCount;

  final String? deadline;

  /// Whether the client's identity is verified.
  ///
  /// **Null when there is no verification field**, and null means the row is
  /// omitted. The old code passed `false` for every job, which rendered
  /// "Not verified" -- an unverified accusation about a real client, printed on
  /// every card in the app. Nothing on this API reports verification state.
  final bool? userVerified;

  final bool fromDetails;

  const ProjectCard(
      {super.key,
      this.id,
      required this.title,
      required this.isFavorite,
      required this.createDate,
      required this.expertise,
      required this.price,
      required this.priceType,
      required this.summery,
      required this.tags,
      required this.location,
      required this.proposalCount,
      required this.deadline,
      this.userVerified,
      this.fromDetails = false});

  @override
  Widget build(BuildContext context) {
    return InkWell(
      onTap: fromDetails
          ? null
          : () {
              // The old code only opened the details screen for `priceType ==
              // "Fixed"`, so range-priced jobs were dead cards: tapping did
              // nothing. `FixedPriceProjectDetailsView` renders both kinds (its
              // price block already handles min/max budgets), so every job
              // opens its details.
              context
                  .toNamed(FixedPriceProjectDetailsView.routeName, arguments: [id, title]);
            },
      child: Container(
        decoration: BoxDecoration(
          borderRadius: BorderRadius.circular(AppRadius.card),
          color: context.dProvider.whiteColor,
        ),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Container(
              padding: const EdgeInsets.all(20),
              decoration: BoxDecoration(
                borderRadius: const BorderRadius.only(
                    topLeft: Radius.circular(6), topRight: Radius.circular(6)),
                color: context.dProvider.whiteColor,
              ),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Row(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Expanded(
                          flex: 30,
                          child: Text(
                            title,
                            style: context.titleMedium?.bold6,
                          )),
                      const Expanded(flex: 1, child: SizedBox()),
                      Expanded(
                        flex: 4,
                        child: const SizedBox(),
                      ),
                    ],
                  ),
                  6.toHeight,
                  RichText(
                      text: TextSpan(
                          text:
                              "${DateFormat("MMM dd,yyyy").format(createDate ?? DateTime.now())} . ",
                          style: context.titleSmall,
                          children: [
                        TextSpan(
                            text: expertise ?? '',
                            style: context.titleSmall
                                ?.copyWith(color: context.dProvider.primaryColor)
                                .bold6)
                      ])),
                  6.toHeight,
                  Row(
                    children: [
                      Text(
                        price == null ? '-' : price.toString().cur,
                        style: context.titleMedium
                            ?.copyWith(color: context.dProvider.primaryColor)
                            .bold6,
                      ),
                      6.toWidth,
                      Container(
                        padding: const EdgeInsets.symmetric(horizontal: 6),
                        decoration: BoxDecoration(
                            borderRadius: BorderRadius.circular(20),
                            border: Border.all(
                              color: context.dProvider.yellowColor,
                            )),
                        child: Text(
                          priceType ?? '',
                          style: context.titleSmall
                              ?.copyWith(color: context.dProvider.yellowColor)
                              .bold6,
                        ),
                      )
                    ],
                  ),
                  6.toHeight,
                  // The description was hidden behind `if (fromDetails)`, so
                  // list cards never showed what the job actually was. Details
                  // renders the full HTML; the list clamps to three plain-text
                  // lines so cards stay scannable (HtmlWidget has no maxLines).
                  if (fromDetails)
                    HtmlWidget(
                      summery ?? '',
                      textStyle:
                          context.titleSmall?.copyWith(color: context.dProvider.black5),
                    )
                  else if (_hasText(summery))
                    Text(
                      _stripHtml(summery!),
                      maxLines: 3,
                      overflow: TextOverflow.ellipsis,
                      style: context.titleSmall
                          ?.copyWith(color: context.dProvider.black5),
                    ),
                  12.toHeight,
                  Wrap(
                    runSpacing: 12,
                    spacing: 12,
                    children: tags.map((tag) {
                      return Container(
                        padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 2),
                        decoration: BoxDecoration(
                          borderRadius: BorderRadius.circular(8),
                          color: context.dProvider.black9,
                        ),
                        child: Text(
                          tag,
                          style: context.bodySmall,
                        ),
                      );
                    }).toList(),
                  ),
                ],
              ),
            ),
            Container(
              width: double.infinity,
              padding: const EdgeInsets.all(20),
              decoration: BoxDecoration(
                borderRadius: const BorderRadius.only(
                    bottomLeft: Radius.circular(6), bottomRight: Radius.circular(6)),
                color: context.dProvider.primary05,
              ),
              child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
                Wrap(
                  runSpacing: 12,
                  spacing: 12,
                  // Each row appears only when there is a real value behind it.
                  children: [
                    if (_hasText(location))
                      Infos(
                          svgIcon: SvgAssets.location
                              .toSVGSized(18, color: context.dProvider.black6),
                          text: location!),
                    // `proposalCount` is checked for null, not falsiness: a genuine
                    // zero is a fact worth showing, an absent count is not.
                    if (proposalCount != null)
                      Infos(
                          svgIcon: SvgAssets.task
                              .toSVGSized(18, color: context.dProvider.black6),
                          text: "${LocalKeys.proposals}: $proposalCount"),
                    if (userVerified != null)
                      Infos(
                          svgIcon: SvgAssets.card
                              .toSVGSized(18, color: context.dProvider.black6),
                          text:
                              userVerified! ? LocalKeys.verified : LocalKeys.notVerified),
                    if (_hasText(deadline))
                      Infos(
                          svgIcon: SvgAssets.clock
                              .toSVGSized(18, color: context.dProvider.black6),
                          text: deadline!.capitalize.tr()),
                  ],
                )
              ]),
            )
          ],
        ),
      ),
    );
  }

  static bool _hasText(String? value) =>
      value != null && value.trim().isNotEmpty && value.trim() != 'null';

  /// Plain-text rendering of a project's HTML description for the list card.
  ///
  /// Strips tags and collapses whitespace. Entities (`&amp;`, `&nbsp;`, …)
  /// survive as-is -- good enough for a clamped preview line, and the details
  /// screen renders the full markup with `HtmlWidget`.
  static String _stripHtml(String html) {
    final withoutTags = html.replaceAll(RegExp(r'<[^>]*>'), ' ');
    return withoutTags
        .replaceAll(RegExp(r'\s+'), ' ')
        .trim();
  }
}

/// Relative timestamp for a project card.
///
/// The private copy of this that lived here was the third divergent version;
/// it now lives in `helper/extension/datetime_extension.dart` as
/// `DateTime.relativeTime`.
String formatDateTime(DateTime date) => date.relativeTime;

class Infos extends StatelessWidget {
  const Infos({
    super.key,
    required this.svgIcon,
    required this.text,
  });

  final text;
  final svgIcon;

  @override
  Widget build(BuildContext context) {
    return FittedBox(
      child: Row(
        children: [svgIcon, 6.toWidth, Text(text)],
      ),
    );
  }
}
