import 'package:flutter/material.dart';
import 'package:provider/provider.dart';
import 'package:webofwork/helper/extension/context_extension.dart';
import 'package:webofwork/helper/extension/datetime_extension.dart';
import 'package:webofwork/helper/extension/int_extension.dart';
import 'package:webofwork/helper/extension/string_extension.dart';
import 'package:webofwork/helper/extension/widget_extension.dart';
import 'package:webofwork/helper/local_keys.g.dart';
import 'package:webofwork/models/contract_model.dart';
import 'package:webofwork/services/contract_details_service.dart';

import 'contract_details_section_card.dart';

/// Change requests raised against submitted work.
///
/// `ContractRevisionResponse` is `{id, contract_id, user_id, reason, status,
/// rejection_message, requested_at, resolved_at}` with `RevisionStatus` of
/// `pending` / `accepted` / `rejected` — the enum this API actually has, in place
/// of the template's revision counter (`revision_left`) the old screen showed.
///
/// Who can do what is split by policy in `RevisionsController`:
///  * `POST /api/contracts/{contractId}/request-revision` is **client only**, so
///    the freelancer never raises a change here — the client does, over chat or
///    their own UI.
///  * `POST /api/revisions/{id}/action` is **freelancer only**, so accepting or
///    rejecting the requested change is exactly this screen's job. It is
///    `PATCH`-shaped as a POST with `{accept, rejection_message}` and answers
///    204.
///
/// The old screen showed a `revision_left` counter and a "request revision"
/// button, which is the opposite assignment and is not available to a
/// freelancer.
class ContractDetailsRevisions extends StatelessWidget {
  const ContractDetailsRevisions({super.key});

  @override
  Widget build(BuildContext context) {
    return Consumer<ContractDetailsService>(builder: (context, od, child) {
      final contract = od.contract;
      if (contract == null) return const SizedBox.shrink();

      return ContractDetailsSectionCard(
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(LocalKeys.revisions, style: context.titleMedium?.bold6).hp20,
            Divider(
              color: context.dProvider.black9,
              thickness: 1,
              height: 24,
            ),
            if (contract.revisions.isEmpty)
              Text(
                LocalKeys.noRevisionsYet,
                style: context.titleSmall?.copyWith(color: context.dProvider.black5),
              ).hp20
            else
              ...contract.revisions.map(
                (r) => _RevisionTile(
                  key: ValueKey(r.id),
                  contractId: contract.id,
                  revision: r,
                ),
              ),
          ],
        ),
      );
    });
  }
}

class _RevisionTile extends StatefulWidget {
  const _RevisionTile({
    super.key,
    required this.contractId,
    required this.revision,
  });

  final int contractId;
  final ContractRevision revision;

  @override
  State<_RevisionTile> createState() => _RevisionTileState();
}

class _RevisionTileState extends State<_RevisionTile> {
  bool _resolving = false;

  @override
  Widget build(BuildContext context) {
    final revision = widget.revision;
    final color = switch (revision.status) {
      ContractRevision.accepted => context.dProvider.greenColor,
      ContractRevision.rejected => context.dProvider.warningColor,
      _ => context.dProvider.primaryColor,
    };

    return Padding(
      padding: const EdgeInsets.only(bottom: 16),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              Container(
                padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 3),
                decoration: BoxDecoration(
                  color: color.withValues(alpha: 0.10),
                  borderRadius: BorderRadius.circular(20),
                ),
                child: Text(
                  // `RevisionStatus` is emitted by
                  // `JsonStringEnumConverter(SnakeCaseLower)`, so this is already
                  // the wire spelling; only capitalise for display.
                  revision.status.isEmpty
                      ? LocalKeys.pending
                      : '${revision.status[0].toUpperCase()}'
                          '${revision.status.substring(1)}',
                  style: context.titleSmall?.copyWith(color: color, fontSize: 12),
                ),
              ),
              const Spacer(),
              if (revision.requestedAt != null)
                Text(
                  revision.requestedAt!.shortDate,
                  style: context.titleSmall?.copyWith(
                    color: context.dProvider.black5,
                    fontSize: 12,
                  ),
                ),
            ],
          ),
          8.toHeight,
          Text(revision.reason, style: context.titleSmall?.black5).hp20,
          if (revision.rejectionMessage != null &&
              revision.rejectionMessage!.trim().isNotEmpty) ...[
            6.toHeight,
            Text(
              revision.rejectionMessage!,
              style: context.titleSmall?.copyWith(color: context.dProvider.warningColor),
            ).hp20,
          ],
          // Only a pending revision can be acted on, and only the freelancer
          // holds the policy that permits it.
          if (revision.isPending) ...[
            8.toHeight,
            Row(
              children: [
                Expanded(
                  child: OutlinedButton(
                    onPressed: _resolving ? null : () => _resolve(accept: false),
                    child: Text(LocalKeys.rejected),
                  ),
                ),
                12.toWidth,
                Expanded(
                  child: ElevatedButton(
                    onPressed: _resolving ? null : () => _resolve(accept: true),
                    child: Text(LocalKeys.accepted),
                  ),
                ),
              ],
            ).hp20,
          ],
          if (_resolving) 12.toHeight,
          if (_resolving) const LinearProgressIndicator().hp20,
        ],
      ),
    );
  }

  /// Rejects ask for a reason first — the server stores it as
  /// `rejection_message` and the client sees it, so an empty rejection would
  /// leave the other side with a bare "rejected".
  Future<void> _resolve({required bool accept}) async {
    String? message;
    if (!accept) {
      message = await _askRejectionReason(context);
      // `null` means the dialog was dismissed, `''` means "reject with no
      // reason" -- which the dialog never returns.
      if (message == null) return;
    }

    if (!mounted) return;
    setState(() => _resolving = true);

    final ok = await context.read<ContractDetailsService>().tryResolveRevision(
          contractId: widget.contractId,
          revisionId: widget.revision.id,
          accept: accept,
          rejectionMessage: message,
        );

    if (!mounted) return;
    setState(() => _resolving = false);
    if (ok) {
      LocalKeys.revisionUpdated.showToast();
    }
  }
}

Future<String?> _askRejectionReason(BuildContext context) async {
  final controller = TextEditingController();
  final result = await showDialog<String>(
    context: context,
    builder: (context) => AlertDialog(
      title: Text(LocalKeys.rejectionReason),
      content: TextField(
        controller: controller,
        autofocus: true,
        maxLines: 3,
        decoration: InputDecoration(hintText: LocalKeys.enterRejectionReason),
      ),
      actions: [
        TextButton(
          onPressed: () => Navigator.of(context).pop(),
          child: Text(LocalKeys.cancel),
        ),
        TextButton(
          onPressed: () => Navigator.of(context).pop(controller.text.trim()),
          child: Text(LocalKeys.submit),
        ),
      ],
    ),
  );
  controller.dispose();
  return result;
}
