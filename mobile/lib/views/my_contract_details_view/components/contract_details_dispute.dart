import 'package:flutter/material.dart';
import 'package:provider/provider.dart';
import 'package:url_launcher/url_launcher.dart' as url_launcher;
import 'package:webofwork/helper/extension/context_extension.dart';
import 'package:webofwork/helper/extension/datetime_extension.dart';
import 'package:webofwork/helper/extension/int_extension.dart';
import 'package:webofwork/helper/extension/string_extension.dart';
import 'package:webofwork/helper/extension/widget_extension.dart';
import 'package:webofwork/helper/local_keys.g.dart';
import 'package:webofwork/models/contract_model.dart';
import 'package:webofwork/services/contract_details_service.dart';

import 'contract_details_section_card.dart';
import 'raise_dispute_sheet.dart';

/// The dispute block of a contract detail, mirroring the reference's
/// "Arbitration Status" card (dispute exists), its "Arbitration Review In
/// Progress" placeholder (disputed but the row has not come back), and its
/// "Raise a Dispute" card (nothing open and the contract is not disputed).
class ContractDetailsDispute extends StatelessWidget {
  const ContractDetailsDispute({super.key});

  @override
  Widget build(BuildContext context) {
    return Consumer<ContractDetailsService>(builder: (context, od, child) {
      final contract = od.contract;
      if (contract == null) return const SizedBox.shrink();

      final dispute = contract.dispute;
      if (dispute != null) {
        return _ArbitrationStatusCard(contract: contract, dispute: dispute);
      }
      if (contract.status == Contract.statusDisputed) {
        return const _ArbitrationInProgressCard();
      }
      return _RaiseDisputeCard(contractId: contract.id);
    });
  }
}

class _RaiseDisputeCard extends StatelessWidget {
  const _RaiseDisputeCard({required this.contractId});

  final int contractId;

  @override
  Widget build(BuildContext context) {
    return ContractDetailsSectionCard(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(
            LocalKeys.raiseDispute,
            style: context.titleMedium?.bold6,
          ).hp20,
          6.toHeight,
          Text(
            LocalKeys.adminDetermination,
            style: context.titleSmall?.black5,
          ).hp20,
          12.toHeight,
          OutlinedButton.icon(
            onPressed: () =>
                showRaiseDisputeSheet(context, contractId: contractId),
            icon: const Icon(Icons.gavel_rounded, size: 18),
            label: Text(LocalKeys.raiseDispute),
          ).hp20,
        ],
      ),
    );
  }
}

class _ArbitrationInProgressCard extends StatelessWidget {
  const _ArbitrationInProgressCard();

  @override
  Widget build(BuildContext context) {
    return ContractDetailsSectionCard(
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Icon(
            Icons.hourglass_top_rounded,
            size: 20,
            color: Colors.amber.shade800,
          ),
          12.toWidth,
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  LocalKeys.arbitrationInProgress,
                  style: context.titleSmall?.bold6,
                ),
                4.toHeight,
                Text(
                  LocalKeys.determinationPendingNote,
                  style: context.titleSmall?.black5,
                ),
              ],
            ),
          ),
        ],
      ).hp20,
    );
  }
}

class _ArbitrationStatusCard extends StatelessWidget {
  const _ArbitrationStatusCard({
    required this.contract,
    required this.dispute,
  });

  final Contract contract;
  final ContractDispute dispute;

  @override
  Widget build(BuildContext context) {
    final statusColor = switch (dispute.status) {
      ContractDispute.statusResolved => const Color(0xFF16A34A),
      ContractDispute.statusRejected => const Color(0xFFDC2626),
      ContractDispute.statusUnderReview => const Color(0xFF2563EB),
      _ => Colors.amber.shade800,
    };

    return ContractDetailsSectionCard(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              Expanded(
                child: Text(
                  LocalKeys.arbitrationStatus,
                  style: context.titleMedium?.bold6,
                ),
              ),
              Container(
                padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 4),
                decoration: BoxDecoration(
                  color: statusColor.withValues(alpha: 0.12),
                  borderRadius: BorderRadius.circular(999),
                ),
                child: Text(
                  dispute.statusLabel,
                  style: context.titleSmall?.copyWith(
                    color: statusColor,
                    fontWeight: FontWeight.w700,
                  ),
                ),
              ),
            ],
          ).hp20,
          12.toHeight,
          _whoRaised(context),
          12.toHeight,
          _kv(context, LocalKeys.caseReference, '#${dispute.id}'),
          8.toHeight,
          _kv(context, LocalKeys.disputeFoundation, dispute.reasonLabel),
          8.toHeight,
          Text(
            dispute.description,
            style: context.titleSmall?.black5,
          ),
          if (dispute.evidenceUrls.isNotEmpty) ...[
            12.toHeight,
            _evidence(context),
          ],
          if (dispute.adminNotes != null && dispute.adminNotes!.trim().isNotEmpty) ...[
            12.toHeight,
            Divider(color: context.dProvider.black9, height: 24),
            Text(
              LocalKeys.adminDetermination,
              style: context.titleSmall?.bold6,
            ),
            4.toHeight,
            Text(
              dispute.adminNotes!,
              style: context.titleSmall?.black5,
            ),
          ],
          if (dispute.createdAt != null) ...[
            12.toHeight,
            _kv(
              context,
              LocalKeys.createdOn,
              dispute.createdAt!.toLocal().shortDate,
            ),
          ],
          if (dispute.resolvedAt != null) ...[
            8.toHeight,
            _kv(
              context,
              LocalKeys.resolvedOnLabel,
              dispute.resolvedAt!.toLocal().shortDate,
            ),
          ],
        ],
      ),
    );
  }

  Widget _whoRaised(BuildContext context) {
    final me = dispute.raisedByMe;
    final color = me ? const Color(0xFFB45309) : const Color(0xFF1D4ED8);
    return Row(
      children: [
        Icon(
          me ? Icons.person_rounded : Icons.person_outline_rounded,
          size: 18,
          color: color,
        ),
        8.toWidth,
        Expanded(
          child: Text(
            me ? LocalKeys.raisedByYou : LocalKeys.raisedByClient,
            style: context.titleSmall?.copyWith(
              color: color,
              fontWeight: FontWeight.w600,
            ),
          ),
        ),
      ],
    );
  }

  Widget _kv(BuildContext context, String label, String value) {
    return Row(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Expanded(
          child: Text(label, style: context.titleSmall?.black5),
        ),
        Flexible(
          child: Text(
            value,
            textAlign: TextAlign.end,
            style: context.titleSmall?.bold6,
          ),
        ),
      ],
    );
  }

  Widget _evidence(BuildContext context) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Text(
          LocalKeys.evidence,
          style: context.titleSmall?.bold6,
        ),
        4.toHeight,
        ...dispute.evidenceUrls.map(
          (url) => InkWell(
            onTap: () => _open(context, url),
            child: Padding(
              padding: const EdgeInsets.symmetric(vertical: 4),
              child: Row(
                children: [
                  const Icon(Icons.open_in_new_rounded, size: 16, color: Color(0xFF2563EB)),
                  8.toWidth,
                  Expanded(
                    child: Text(
                      url,
                      maxLines: 1,
                      overflow: TextOverflow.ellipsis,
                      style: context.titleSmall?.copyWith(
                        color: const Color(0xFF2563EB),
                      ),
                    ),
                  ),
                ],
              ),
            ),
          ),
        ),
      ],
    );
  }

  Future<void> _open(BuildContext context, String url) async {
    final uri = Uri.tryParse(url);
    if (uri == null || !await url_launcher.canLaunchUrl(uri)) {
      LocalKeys.invalidUrl.showToast();
      return;
    }
    await url_launcher.launchUrl(uri,
        mode: url_launcher.LaunchMode.externalApplication);
  }
}