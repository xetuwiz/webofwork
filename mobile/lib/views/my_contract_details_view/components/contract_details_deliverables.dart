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
import 'submit_deliverable_sheet.dart';
import 'edit_deliverable_sheet.dart';

/// Work submitted against a contract.
///
/// This replaces the template's "Milestones" + "Submitted work" pair, which had no
/// backing data at all: `orderDetails.orderSubmitHistory!` and
/// `orderDetails.orderMileStones!` were both unguarded bang-dereferences onto
/// fields the template's `Order` model defined but `ContractResponse` never sent,
/// so this tab threw on every contract.
///
/// The API's actual unit of work is a `Deliverable`
/// (`POST /api/contracts/{id}/submit-work`, `GET /api/deliverables/contract/{id}`),
/// and it is embedded in `ContractResponse.Deliverables`, so the list needs no
/// extra request.
class ContractDetailsDeliverables extends StatelessWidget {
  const ContractDetailsDeliverables({super.key});

  @override
  Widget build(BuildContext context) {
    return Consumer<ContractDetailsService>(builder: (context, od, child) {
      final contract = od.contract;
      if (contract == null) return const SizedBox.shrink();

      return ContractDetailsSectionCard(
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(
              mainAxisAlignment: MainAxisAlignment.spaceBetween,
              children: [
                Text(
                  LocalKeys.deliverables,
                  style: context.titleMedium?.bold6,
                ),
                // A contract only accepts work once **both** parties have signed;
                // offering the button earlier produced a server-side rejection.
                if (contract.acceptsWork)
                  TextButton.icon(
                    onPressed: () => _openSubmitSheet(context, contract),
                    icon: const Icon(Icons.add_rounded, size: 18),
                    label: Text(LocalKeys.submitWork),
                  ),
              ],
            ).hp20,
            Divider(
              color: context.dProvider.black9,
              thickness: 1,
              height: 24,
            ),
            if (contract.deliverables.isEmpty)
              Text(
                LocalKeys.noDeliverablesYet,
                style: context.titleSmall?.copyWith(color: context.dProvider.black5),
              ).hp20
            else
              ...contract.deliverables.map(
                (d) => _DeliverableTile(
                  key: ValueKey(d.id),
                  deliverable: d,
                  canManage: contract.canManageDeliverables,
                  onEdit: () => showEditDeliverableSheet(context, deliverable: d),
                  onDelete: () => _confirmDelete(context, d),
                ),
              ),
            if (contract.expectedDeliverables != null &&
                contract.expectedDeliverables!.trim().isNotEmpty) ...[
              Divider(
                color: context.dProvider.black9,
                thickness: 1,
                height: 24,
              ),
              Text(
                LocalKeys.expectedDeliverables,
                style: context.titleSmall?.bold6,
              ).hp20,
              6.toHeight,
              Text(
                contract.expectedDeliverables!,
                style: context.titleSmall?.black5,
              ).hp20,
            ],
          ],
        ),
      );
    });
  }

  Future<void> _openSubmitSheet(BuildContext context, Contract contract) async {
    // `trySubmitOrder` folds the created deliverable into the loaded contract and
    // notifies its own listeners, so a success needs nothing further here.
    await showSubmitDeliverableSheet(context, contractId: contract.id);
  }

  Future<void> _confirmDelete(
      BuildContext context, ContractDeliverable deliverable) async {
    final confirmed = await showDialog<bool>(
      context: context,
      builder: (dialogContext) => AlertDialog(
        title: Text(LocalKeys.deleteDeliverableTitle),
        content: Text(LocalKeys.deleteDeliverableConfirm),
        actions: [
          TextButton(
            onPressed: () => Navigator.of(dialogContext).pop(false),
            child: Text(LocalKeys.cancel),
          ),
          TextButton(
            onPressed: () => Navigator.of(dialogContext).pop(true),
            child: Text(
              LocalKeys.deleteDeliverable,
              style: TextStyle(color: Theme.of(dialogContext).colorScheme.error),
            ),
          ),
        ],
      ),
    );
    if (confirmed != true || !context.mounted) return;
    await context.read<ContractDetailsService>().tryDeleteDeliverable(deliverable.id);
  }
}

class _DeliverableTile extends StatelessWidget {
  const _DeliverableTile({
    super.key,
    required this.deliverable,
    required this.canManage,
    required this.onEdit,
    required this.onDelete,
  });

  final ContractDeliverable deliverable;

  /// Shown only while the contract is `active`/`submitted` — the same window
  /// the server's `EnsureMutable` gate allows edits and deletes in.
  final bool canManage;
  final VoidCallback onEdit;
  final VoidCallback onDelete;

  @override
  Widget build(BuildContext context) {
    final attachment = deliverable.attachment;
    return Padding(
      padding: const EdgeInsets.only(bottom: 16),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Icon(
                deliverable.isLink
                    ? Icons.link_rounded
                    : Icons.insert_drive_file_outlined,
                size: 20,
                color: context.dProvider.black5,
              ),
              12.toWidth,
              Expanded(
                child: Text(
                  deliverable.title,
                  style: context.titleSmall?.bold6,
                ),
              ),
              if (attachment != null)
                TextButton(
                  onPressed: () => _open(context, attachment),
                  child: Text(
                    deliverable.isLink ? LocalKeys.openLink : LocalKeys.view,
                  ),
                ),
              if (canManage) ...[
                IconButton(
                  onPressed: onEdit,
                  visualDensity: VisualDensity.compact,
                  tooltip: LocalKeys.editDeliverable,
                  icon: const Icon(Icons.edit_outlined, size: 20),
                ),
                IconButton(
                  onPressed: onDelete,
                  visualDensity: VisualDensity.compact,
                  tooltip: LocalKeys.deleteDeliverable,
                  icon: Icon(
                    Icons.delete_outline_rounded,
                    size: 20,
                    color: Theme.of(context).colorScheme.error,
                  ),
                ),
              ],
            ],
          ),
          if (deliverable.notes != null && deliverable.notes!.trim().isNotEmpty) ...[
            4.toHeight,
            Text(
              deliverable.notes!,
              style: context.titleSmall?.black5,
            ).hp32,
          ],
          if (deliverable.submittedAt != null) ...[
            4.toHeight,
            Text(
              deliverable.submittedAt!.friendlyDateTime,
              style: context.titleSmall
                  ?.copyWith(color: context.dProvider.black5, fontSize: 12),
            ).hp32,
          ],
        ],
      ),
    );
  }

  Future<void> _open(BuildContext context, String url) async {
    final uri = Uri.tryParse(url);
    if (uri == null || !await url_launcher.canLaunchUrl(uri)) {
      LocalKeys.invalidUrl.showToast();
      return;
    }
    await url_launcher.launchUrl(uri, mode: url_launcher.LaunchMode.externalApplication);
  }
}
