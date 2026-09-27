import 'package:flutter/material.dart';
import 'package:webofwork/utils/components/app_card.dart';
import 'package:webofwork/helper/extension/context_extension.dart';
import 'package:webofwork/helper/extension/int_extension.dart';
import 'package:webofwork/helper/extension/widget_extension.dart';
import 'package:webofwork/helper/local_keys.g.dart';
import 'package:webofwork/models/contract_model.dart';

import 'my_contract_card_infos.dart';

/// One row in the contracts list.
///
/// Takes a parsed [Contract] rather than the dozen loosely-typed template
/// parameters it used to take (`orderType`, `projectStatus`, `isCustom`, …), because
/// every one of those had to be mapped from fields `ContractResponse` does not
/// carry. The whole card was therefore rendering `null`s: no title, no amount,
/// no client name, and an `Icon` sized from a `double?` that was always null.
class MyContractCard extends StatelessWidget {
  const MyContractCard({super.key, required this.contract});

  final Contract contract;

  @override
  Widget build(BuildContext context) {
    return InkWell(
      onTap: () => onMyOrderTap(context, contract.id),
      borderRadius: BorderRadius.circular(AppRadius.card),
      child: Container(
        width: double.infinity,
        margin: const EdgeInsets.only(bottom: 12),
        padding: const EdgeInsets.symmetric(vertical: 20),
        decoration: BoxDecoration(
          borderRadius: BorderRadius.circular(AppRadius.card),
          color: context.dProvider.whiteColor,
          border: Border.all(color: context.dProvider.black9),
        ),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            MyContractCardInfos(contract: contract),
            12.toHeight,
            Row(
              children: [
                Text(
                  LocalKeys.viewOrder,
                  style: context.titleSmall?.copyWith(
                    color: context.dProvider.primaryColor,
                  ),
                ),
                4.toWidth,
                Icon(
                  Icons.chevron_right_rounded,
                  size: 18,
                  color: context.dProvider.primaryColor,
                ),
              ],
            ).hp20,
          ],
        ),
      ),
    );
  }
}

/// Pushes the details screen for [contractId].
///
/// The previous card built a **new** `GlobalKey` on every build and attached it
/// to the row, then looked that key up on tap. Because the key was created in
/// `build`, a rebuild produced a different key, so a lookup frequently missed
/// and the tap did nothing. The screen is a route push keyed by id, so the
/// widget tree is not needed at all.
void onMyOrderTap(BuildContext context, int contractId) {
  Navigator.of(context).pushNamed(
    'my_contract_details_view',
    arguments: contractId,
  );
}
