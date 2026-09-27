import 'package:flutter/material.dart';
import 'package:provider/provider.dart';
import 'package:webofwork/helper/extension/int_extension.dart';
import 'package:webofwork/helper/extension/context_extension.dart';
import 'package:webofwork/helper/local_keys.g.dart';
import 'package:webofwork/services/contract_details_service.dart';
import 'package:webofwork/utils/components/custom_future_widget.dart';
import 'package:webofwork/utils/components/custom_refresh_indicator.dart';
import 'package:webofwork/utils/components/navigation_pop_icon.dart';
import 'package:webofwork/views/my_contract_details_view/components/contract_details_deliverables.dart';
import 'package:webofwork/views/my_contract_details_view/components/contract_details_description.dart';
import 'package:webofwork/views/my_contract_details_view/components/contract_details_dispute.dart';
import 'package:webofwork/views/my_contract_details_view/components/contract_details_revisions.dart';
import 'package:webofwork/views/my_contract_details_view/components/contract_details_timeline.dart';
import 'package:webofwork/views/my_contract_details_view/components/contract_details_agreement.dart';
import 'package:webofwork/views/my_contract_details_view/components/contract_details_reviews.dart';
import 'package:webofwork/views/my_contracts_view/components/my_contract_card_infos.dart';

import 'components/my_contract_details_skeleton.dart';

/// A single contract.
///
/// Sections are stacked rather than tabbed. The previous version drove a
/// horizontal tab strip from `MyOrderDetailsViewModel.instance.selectedTitleIndex`
/// — a `ValueNotifier` on a **process-wide singleton** — so the selected tab was
/// shared by every open instance of the screen and survived being popped. Opening
/// a contract from a notification, then backing out and opening another one,
/// landed on the tab the *previous* contract was left on. The tabs themselves
/// were `Description | Milestones | Submitted work`, and `Milestones` was
/// conditional on `orderMileStones` being non-null -- it was always null, so the
/// middle tab never appeared and the indices in the tab strip and the tab bodies
/// could disagree.
class MyContractDetailsView extends StatelessWidget {
  const MyContractDetailsView() : super(key: const Key("my_order_details_view"));

  static const routeName = 'my_contract_details_view';

  @override
  Widget build(BuildContext context) {
    final odProvider = Provider.of<ContractDetailsService>(context, listen: false);

    // A notification deep-links here with the contract id as the route argument.
    // A malformed link can land with a null or non-numeric id, which
    // `fetchOrderDetails` now rejects rather than requesting contract 0.
    final orderId = ModalRoute.of(context)?.settings.arguments;

    return Scaffold(
      appBar: AppBar(
        leading: const NavigationPopIcon(),
        title: Text(LocalKeys.myOrder),
      ),
      body: CustomRefreshIndicator(
        onRefresh: () => odProvider.fetchOrderDetails(orderId: orderId),
        child: CustomFutureWidget(
          function: odProvider.shouldAutoFetch(orderId)
              ? () => odProvider.fetchOrderDetails(orderId: orderId)
              : null,
          shimmer: const MyContractSkeleton(),
          child: Consumer<ContractDetailsService>(builder: (context, od, child) {
            final contract = od.contract;
            if (contract == null) {
              // Previously `od.orderDetailsModel.orderDetails!` was
              // force-unwrapped here, so any failed fetch -- a 404 on a stale
              // notification link, an expired session -- threw and left a blank
              // red screen.
              return ListView(
                padding: const EdgeInsets.all(20),
                children: [
                  120.toHeight,
                  Text(
                    LocalKeys.contractNotLoaded,
                    textAlign: TextAlign.center,
                    style: context.titleMedium,
                  ),
                ],
              );
            }

            return ListView(
              padding: const EdgeInsets.all(20),
              children: [
                MyContractCardInfos(contract: contract, fromDetails: true),
                20.toHeight,
                // Dispute block first: a live dispute pauses the whole
                // workspace, and the reference leads with it.
                const ContractDetailsDispute(),
                20.toHeight,
                // Reviews only appear on a settled contract (completed /
                // refunded), mirroring the server's Completed|Refunded gate.
                const ContractDetailsReviews(),
                20.toHeight,
                const ContractDetailsDescription(),
                20.toHeight,
                const ContractDetailsDeliverables(),
                20.toHeight,
                const ContractDetailsRevisions(),
                20.toHeight,
                const ContractDetailsTimeline(),
                20.toHeight,
                const ContractDetailsAgreement(),
                20.toHeight,
              ],
            );
          }),
        ),
      ),
    );
  }
}
