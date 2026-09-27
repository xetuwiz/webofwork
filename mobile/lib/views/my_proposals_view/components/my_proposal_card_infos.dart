import 'package:flutter/material.dart';
import 'package:webofwork/helper/extension/context_extension.dart';
import 'package:webofwork/helper/extension/datetime_extension.dart';
import 'package:webofwork/helper/extension/int_extension.dart';
import 'package:webofwork/helper/extension/string_extension.dart';
import 'package:webofwork/helper/extension/widget_extension.dart';
import 'package:webofwork/helper/local_keys.g.dart';
import 'package:webofwork/models/my_proposals_model.dart';
import 'package:webofwork/views/my_contracts_view/components/my_contract_id.dart';

/// The proposal summary shown in the My Proposals list.
///
/// Built from the fields `ProposalResponse` actually carries
/// (`DTOs/Proposals/ProposalDtos.cs`): `project_title`, `bid_amount`,
/// `delivery_days`, `cover_letter`, `status`, `created_at`, `contract_info`.
///
/// Deliberately **not** shown, because this API has no such field and showing a
/// fabricated value is worse than showing nothing:
///  * a "seen / not seen" chip -- there is no read-tracking on a proposal.
///  * a "shortlisted" or "interviewed" chip -- template concepts.
///  * an attachment -- `CreateProposalRequest` has no attachment and
///    `ProposalResponse` has no path, so the old code built
///    `"${path}/${attachment}"` from two values that were always empty.
///
/// The status chip replaces five dead boolean flags with the one field the
/// server sends.
class MyProposalCardInfos extends StatelessWidget {
  const MyProposalCardInfos({
    super.key,
    required this.proposal,
  });

  final MyProposal proposal;

  @override
  Widget build(BuildContext context) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        MyContractId(id: "#${proposal.id}").hp20,
        if (proposal.projectTitle.isNotEmpty) ...[
          8.toHeight,
          Text(
            proposal.projectTitle,
            style: context.titleMedium?.bold6,
          ).hp20,
        ],
        8.toHeight,
        Wrap(
          spacing: 8,
          runSpacing: 8,
          children: [
            _Chip(
              label: proposal.statusLabel,
              foreground: _statusColor(context),
              background: _statusColor(context).withValues(alpha: 0.10),
            ),
            // `ProposalContractInfo` is the server telling us the client already
            // acted on this proposal. Surfacing it stops a freelancer from
            // re-applying to a project they have already been hired for.
            if (proposal.contractInfo != null)
              _Chip(
                label: LocalKeys.contractCreated,
                foreground: context.dProvider.greenColor,
                background: context.dProvider.greenColor.withValues(alpha: 0.10),
              ),
          ],
        ).hp20,
        if (proposal.createdAt != null) ...[
          8.toHeight,
          Text(
            proposal.createdAt!.friendlyDateTime,
            style: context.titleSmall?.copyWith(color: context.dProvider.black5),
          ).hp20,
        ],
        const Divider(height: 24, thickness: 1),
        _Row(
          label: LocalKeys.offerPrice,
          value: proposal.bidAmount.toString().cur,
        ),
        if (proposal.deliveryDays > 0)
          _Row(
            label: LocalKeys.deliveryTime,
            value: '${proposal.deliveryDays} ${LocalKeys.days}',
          ),
        if (proposal.packageId.isNotEmpty)
          _Row(
            label: LocalKeys.package,
            value: proposal.packageId,
          ),
        if (proposal.coverLetter.isNotEmpty) ...[
          const Divider(height: 24, thickness: 1),
          Text(
            proposal.coverLetter,
            maxLines: 4,
            overflow: TextOverflow.ellipsis,
            style: context.titleSmall?.copyWith(color: context.dProvider.black5),
          ).hp20,
        ],
      ],
    );
  }

  Color _statusColor(BuildContext context) => switch (proposal.status) {
        MyProposal.statusPending => context.dProvider.primaryColor,
        MyProposal.statusAccepted => context.dProvider.greenColor,
        // `warningColor` is this palette's red (0xFFEB4747); there is no
        // separate `redColor`.
        MyProposal.statusRejected => context.dProvider.warningColor,
        MyProposal.statusWithdrawn => context.dProvider.black5,
        _ => context.dProvider.black5,
      };
}

class _Chip extends StatelessWidget {
  const _Chip({
    required this.label,
    required this.foreground,
    required this.background,
  });

  final String label;
  final Color foreground;
  final Color background;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 4),
      decoration: BoxDecoration(
        color: background,
        borderRadius: BorderRadius.circular(20),
      ),
      child: Text(
        label,
        style: context.titleSmall?.copyWith(color: foreground),
      ),
    );
  }
}

class _Row extends StatelessWidget {
  const _Row({required this.label, required this.value});

  final String label;
  final String value;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.symmetric(vertical: 8),
      child: Row(
        children: [
          Expanded(
            flex: 2,
            child: Text(
              label,
              style: context.titleSmall?.copyWith(color: context.dProvider.black5),
            ),
          ),
          Expanded(
            flex: 3,
            child: Text(
              value,
              maxLines: 2,
              overflow: TextOverflow.ellipsis,
              textAlign: TextAlign.end,
              style: context.titleSmall?.bold6,
            ),
          ),
        ],
      ),
    ).hp20;
  }
}
