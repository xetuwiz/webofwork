import 'package:flutter/material.dart';
import 'package:webofwork/helper/extension/context_extension.dart';
import 'package:webofwork/helper/extension/datetime_extension.dart';
import 'package:webofwork/helper/extension/int_extension.dart';
import 'package:webofwork/helper/extension/string_extension.dart';
import 'package:webofwork/helper/extension/widget_extension.dart';
import 'package:webofwork/helper/local_keys.g.dart';
import 'package:webofwork/models/contract_model.dart';
import 'package:webofwork/views/my_contract_details_view/components/earning_tiles.dart';
import 'package:webofwork/views/my_contract_details_view/components/contract_details_buttons.dart';
import 'package:webofwork/views/my_contracts_view/components/my_contract_id.dart';

import '../../chat_list_view/components/chat_tile_avatar.dart';

/// The contract summary block, shared by the orders list card and the details
/// screen header.
///
/// Takes a parsed [Contract] rather than a dozen loose fields. It previously
/// took `id`, `title`, `price`, `isProjectJob`, `isCustom`, `deliveryTime`,
/// `rating`, `paymentStatus`, `createdAt`, `customerImage`… and the two call
/// sites each had to map template-shaped data into them, which is how fields
/// that this API does not have (`isProjectJob`, `createdAt`, a client avatar,
/// a per-order `rating`) ended up on screen as `null`, `"null"` or an empty
/// string.
///
/// Notes on what is deliberately **not** shown:
///  * **A client avatar.** `ContractResponse` carries `client_id` and
///    `client_name` but no photo, so the avatar falls back to initials derived
///    from the name. The old code built the path as
///    `"${imagePath}/${user?.image}"`, which rendered a broken image.
///  * **A per-contract rating.** `ContractResponse.Reviews` is emitted for the
///    `Big` profile payload (`ProfilesController.Big`), not inside a contract
///    row, so a per-order rating shown here would be fabricated.
class MyContractCardInfos extends StatelessWidget {
  const MyContractCardInfos({
    super.key,
    required this.contract,
    this.fromDetails = false,
  });

  final Contract contract;

  /// Adds the earnings tiles and the action buttons, which only make sense on
  /// the full screen.
  final bool fromDetails;

  @override
  Widget build(BuildContext context) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        MyContractId(id: "#${contract.id}").hp20,
        if (contract.projectTitle.isNotEmpty) ...[
          8.toHeight,
          Text(
            contract.projectTitle,
            style: context.titleMedium?.bold6,
          ).hp20,
        ],
        8.toHeight,
        Wrap(
          spacing: 8,
          runSpacing: 8,
          children: [
            _Chip(
              label: contract.statusLabel,
              foreground: _statusColor(context),
              background: _statusColor(context).withValues(alpha: 0.10),
            ),
            if (contract.packageName.isNotEmpty)
              _Chip(
                label: contract.packageName,
                foreground: context.dProvider.black5,
                background: context.dProvider.black9,
              ),
            if (contract.isFunded)
              _Chip(
                label: LocalKeys.funded,
                foreground: context.dProvider.greenColor,
                background: context.dProvider.greenColor.withValues(alpha: 0.10),
              ),
          ],
        ).hp20,
        if (contract.completedAt != null) ...[
          8.toHeight,
          Text(
            '${LocalKeys.completedOn} ${contract.completedAt!.friendlyDateTime}',
            style: context.titleSmall?.copyWith(color: context.dProvider.black5),
          ).hp20,
        ],
        if (contract.createdAt != null) ...[
          8.toHeight,
          Text(
            '${LocalKeys.createdOn} ${contract.createdAt!.friendlyDateTime}',
            style: context.titleSmall?.copyWith(color: context.dProvider.black5),
          ).hp20,
        ],
        const Divider(height: 24, thickness: 1),
        _Row(
          label: LocalKeys.orderBudget,
          value: contract.totalAmount.toString().cur,
        ),
        // `payout_eligible_after` only exists once the contract is funded.
        if (contract.payoutEligibleAfter != null)
          _Row(
            label: LocalKeys.payoutEligibleAfter,
            value: contract.payoutEligibleAfter!.shortDate,
          ),
        if (contract.deliveryDays > 0)
          _Row(
            label: LocalKeys.deliveryTime,
            value: '${contract.deliveryDays} ${LocalKeys.days}',
          ),
        if (contract.clientName.isNotEmpty)
          _Row(
            label: LocalKeys.customer,
            leading: ChatTileAvatar(name: contract.clientName, size: 32),
            value: contract.clientName,
          ),
        if (fromDetails) ...[
          12.toHeight,
          const EarningTiles(),
          12.toHeight,
          ContractDetailsButtons(contract: contract),
        ],
      ],
    );
  }

  /// Colour-codes the status chip.
  ///
  /// Replaces `orderStatus.toString() == "2" || == "1"` style checks against
  /// the template's integer statuses. `ContractStatus` is serialised as a
  /// snake_case *string* by `JsonStringEnumConverter(SnakeCaseLower)`, so those
  /// comparisons never matched anything -- which is why the dashboard's
  /// active-contracts list was always empty.
  Color _statusColor(BuildContext context) => switch (contract.status) {
        Contract.statusActive => context.dProvider.greenColor,
        Contract.statusCompleted => context.dProvider.greenColor,
        Contract.statusOffered => context.dProvider.primaryColor,
        Contract.statusSubmitted => context.dProvider.primaryColor,
        Contract.statusDisputed => context.dProvider.warningColor,
        // `warningColor` is this palette's red (0xFFEB4747); there is no
        // separate `redColor`.
        Contract.statusCancelled => context.dProvider.warningColor,
        Contract.statusRefunded => context.dProvider.warningColor,
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
  const _Row({
    required this.label,
    required this.value,
    this.leading,
  });

  final String label;
  final String value;
  final Widget? leading;

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
              style: context.titleSmall?.black5,
            ),
          ),
          Expanded(
            flex: 3,
            child: Row(
              children: [
                if (leading != null) ...[
                  leading!,
                  12.toWidth,
                ],
                Expanded(
                  child: Text(
                    value,
                    maxLines: 1,
                    overflow: TextOverflow.ellipsis,
                    style: context.titleSmall?.bold6,
                  ),
                ),
              ],
            ),
          ),
        ],
      ),
    );
  }
}
