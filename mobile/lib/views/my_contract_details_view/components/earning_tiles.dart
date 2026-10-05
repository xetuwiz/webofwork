import 'package:flutter/material.dart';
import 'package:provider/provider.dart';
import 'package:webofwork/helper/extension/context_extension.dart';
import 'package:webofwork/helper/extension/widget_extension.dart';
import 'package:webofwork/helper/local_keys.g.dart';
import 'package:webofwork/services/contract_details_service.dart';

import 'hourly_price_info_tile.dart';

/// The four money figures for a contract.
///
/// Built from the fields `ContractResponse` actually has. The previous version
/// read `commission_amount`, `payable_amount` and compared
/// `status.toString() == "3"` -- all template concepts. `ContractResponse` has no
/// commission column at all, and the platform's cut is exactly
/// `total_amount - freelancer_earnings`, so the split is derived from the two
/// amounts the server does send rather than shown as a constant zero.
class EarningTiles extends StatelessWidget {
  const EarningTiles({super.key});

  @override
  Widget build(BuildContext context) {
    return Consumer<ContractDetailsService>(builder: (context, od, child) {
      final contract = od.contract;
      if (contract == null) return const SizedBox.shrink();

      // The platform's cut. Negative (or a rounding artefact above the total)
      // would mean the server's earnings figure exceeds the contract value, so
      // it is clamped rather than displayed as a negative fee.
      final fee = (contract.totalAmount - contract.freelancerEarnings)
          .clamp(0, contract.totalAmount)
          .toDouble();

      return Wrap(
        spacing: 8,
        runSpacing: 8,
        children: [
          HourlyPriceInfoTile(
            price: contract.paidToFreelancer ? contract.freelancerEarnings : 0,
            priceNote: '',
            status: LocalKeys.earnedBalance,
            color: context.dProvider.gridColors[0],
            desc: LocalKeys.earnedBalanceDesc,
          ),
          HourlyPriceInfoTile(
            price: contract.paidToFreelancer ? 0 : contract.freelancerEarnings,
            priceNote: '',
            status: LocalKeys.pendingBalance,
            color: context.dProvider.gridColors[1],
            desc: LocalKeys.pendingBalanceDesc,
          ),
          HourlyPriceInfoTile(
            price: fee,
            priceNote: '',
            status: LocalKeys.commissionAmount,
            color: context.dProvider.gridColors[2],
            desc: LocalKeys.commissionBalanceDesc,
          ),
          HourlyPriceInfoTile(
            price: contract.totalAmount,
            priceNote: '',
            status: LocalKeys.totalBudget,
            color: context.dProvider.gridColors[3],
            desc: LocalKeys.totalBudgetDesc,
          ),
        ],
      ).hp20;
    });
  }
}
