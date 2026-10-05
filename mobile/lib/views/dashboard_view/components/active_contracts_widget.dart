import 'package:flutter/material.dart';
import 'package:provider/provider.dart';
import 'package:webofwork/helper/extension/context_extension.dart';
import 'package:webofwork/services/my_contracts_list_service.dart';
import 'package:webofwork/views/my_contracts_view/components/my_contract_card.dart';

/// The dashboard's "active contracts" strip: up to three live contracts.
///
/// The filter used to be `o.status == 1 || o.status == 2` against the template's
/// **integer** statuses. `ContractStatus` is emitted by
/// `JsonStringEnumConverter(SnakeCaseLower)`, so `status` is the string
/// `"active"` / `"submitted"` and an `int` comparison is never true -- the widget
/// could only ever have shown its "No active contracts right now" placeholder.
class ActiveContractsWidget extends StatefulWidget {
  const ActiveContractsWidget({super.key});

  @override
  State<ActiveContractsWidget> createState() => _ActiveContractsWidgetState();
}

class _ActiveContractsWidgetState extends State<ActiveContractsWidget> {
  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addPostFrameCallback((_) {
      if (!mounted) return;
      final s = context.read<MyContractsListService>();
      if (s.shouldAutoFetch) {
        s.fetchOrderList();
      }
    });
  }

  @override
  Widget build(BuildContext context) {
    return Consumer<MyContractsListService>(
      builder: (context, ms, child) {
        if (ms.isLoading && ms.isEmpty) {
          return const Center(child: CircularProgressIndicator());
        }

        // `isLive` covers `active`, `submitted` and `disputed`. A contract
        // that is merely `offered` is not active work, so it is left for the
        // offers screen.
        final activeContracts = ms.orderList.where((c) => c.isLive).take(3).toList();

        if (activeContracts.isEmpty) {
          return Padding(
            padding: const EdgeInsets.symmetric(vertical: 24),
            child: Center(
              child: Text(
                'No active contracts right now',
                style: context.titleSmall?.copyWith(color: context.dProvider.black5),
              ),
            ),
          );
        }

        return Column(
          children: activeContracts
              .map(
                (contract) => Padding(
                  padding: const EdgeInsets.only(bottom: 12.0),
                  child: MyContractCard(contract: contract),
                ),
              )
              .toList(),
        );
      },
    );
  }
}
