import 'package:flutter/material.dart';
import 'package:provider/provider.dart';
import 'package:webofwork/helper/extension/context_extension.dart';
import 'package:webofwork/helper/extension/datetime_extension.dart';
import 'package:webofwork/helper/extension/int_extension.dart';
import 'package:webofwork/helper/extension/widget_extension.dart';
import 'package:webofwork/helper/local_keys.g.dart';
import 'package:webofwork/models/contract_model.dart';
import 'package:webofwork/services/contract_details_service.dart';
import 'package:webofwork/utils/components/star_rating.dart';

import 'contract_details_section_card.dart';
import 'submit_review_sheet.dart';

/// The two review cards on a settled contract (`completed`/`refunded`), mirroring
/// the reference's "Client's feedback" and "Your review for client" pair.
///
/// The server embeds `reviews.client_review` / `reviews.freelancer_review` on
/// the contract response; `client_review` is the client's rating of the
/// freelancer (`ReviewType.Freelancer`), `freelancer_review` is this user's
/// rating of the client. When the freelancer has not reviewed yet, a prompt
/// opens the review sheet.
class ContractDetailsReviews extends StatelessWidget {
  const ContractDetailsReviews({super.key});

  @override
  Widget build(BuildContext context) {
    return Consumer<ContractDetailsService>(builder: (context, od, child) {
      final contract = od.contract;
      if (contract == null) return const SizedBox.shrink();
      // The server refuses reviews on any other status; mirror the gate.
      if (!contract.canSubmitReview) return const SizedBox.shrink();

      return ContractDetailsSectionCard(
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(LocalKeys.reviews, style: context.titleMedium?.bold6).hp20,
            16.toHeight,
            _ReviewBlock(
              title: LocalKeys.clientsFeedback,
              review: contract.clientReview,
              emptyText: LocalKeys.clientHasntReviewedYet,
            ),
            16.toHeight,
            Divider(color: context.dProvider.black9, height: 24),
            4.toHeight,
            _ReviewBlock(
              title: LocalKeys.yourReviewForClient,
              review: contract.freelancerReview,
              emptyText: LocalKeys.rateYourExperience,
              contractId: contract.id,
            ),
          ],
        ),
      );
    });
  }
}

class _ReviewBlock extends StatelessWidget {
  const _ReviewBlock({
    required this.title,
    required this.review,
    required this.emptyText,
    this.contractId,
  });

  final String title;
  final ContractReview? review;
  final String emptyText;

  /// Non-null when this block is the freelancer's own review slot, so the
  /// empty state becomes a button that opens the sheet.
  final int? contractId;

  @override
  Widget build(BuildContext context) {
    final r = review;
    return Padding(
      padding: const EdgeInsets.symmetric(horizontal: 20),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(title, style: context.titleSmall?.bold6),
          6.toHeight,
          if (r == null)
            contractId != null
                ? OutlinedButton.icon(
                    onPressed: () => showSubmitReviewSheet(
                      context,
                      contractId: contractId!,
                    ),
                    icon: const Icon(Icons.star_outline_rounded, size: 18),
                    label: Text(LocalKeys.rateYourExperience),
                  )
                : Text(
                    emptyText,
                    style: context.titleSmall
                        ?.copyWith(color: context.dProvider.black5),
                  )
          else ...[
            Row(
              children: [
                StarRating(rating: r.rating, size: 18),
                8.toWidth,
                if (r.reviewerName != null && r.reviewerName!.isNotEmpty)
                  Expanded(
                    child: Text(
                      r.reviewerName!,
                      maxLines: 1,
                      overflow: TextOverflow.ellipsis,
                      style: context.titleSmall?.black5,
                    ),
                  ),
              ],
            ),
            if (r.comment.trim().isNotEmpty) ...[
              6.toHeight,
              Text(r.comment, style: context.titleSmall?.black5),
            ],
            if (r.createdAt != null) ...[
              4.toHeight,
              Text(
                '${LocalKeys.reviewedOnLabel} ${r.createdAt!.toLocal().shortDate}',
                style: context.titleSmall?.copyWith(
                  color: context.dProvider.black5,
                  fontSize: 12,
                ),
              ),
            ],
          ],
        ],
      ),
    );
  }
}