import 'package:flutter/material.dart';
import 'package:provider/provider.dart';
import 'package:webofwork/helper/extension/context_extension.dart';
import 'package:webofwork/helper/extension/int_extension.dart';
import 'package:webofwork/helper/local_keys.g.dart';
import 'package:webofwork/models/contract_model.dart';
import 'package:webofwork/services/my_contracts_list_service.dart';
import 'package:webofwork/utils/components/custom_future_widget.dart';
import 'package:webofwork/utils/components/custom_refresh_indicator.dart';
import 'package:webofwork/views/my_contracts_view/components/my_contract_card.dart';

import '../../services/profile_info_service.dart';
import '../../utils/components/custom_preloader.dart';
import '../../utils/components/empty_widget.dart';
import '../account_skeleton/account_skeleton.dart';
import 'components/my_contracts_skeleton.dart';

/// The freelancer's contracts.
///
/// `StatefulWidget` rather than `StatelessWidget`: the previous version called
/// `mom.scrollController.addListener(...)` from inside `build`, so every
/// rebuild -- and this tree rebuilds on each of three `Consumer`s -- added
/// *another* listener to the same controller. The listener was never removed,
/// so after a handful of navigations a single scroll fired the pagination
/// trigger many times over, firing that many overlapping `fetchNextPage` calls.
///
/// Status tabs and a search box now drive `?status=` / `?search=` on
/// `GET /api/contracts` -- the server supported both, but the screen only ever
/// asked for the unfiltered list, so there was no way to find one contract
/// among dozens, and "disputed" work was buried in the same pile as everything
/// else.
class MyContractsView extends StatefulWidget {
  const MyContractsView({super.key});

  static const routeName = 'my_contracts_view';

  @override
  State<MyContractsView> createState() => _MyContractsViewState();
}

class _MyContractsViewState extends State<MyContractsView> {
  final ScrollController _scrollController = ScrollController();
  final TextEditingController _searchController = TextEditingController();

  @override
  void initState() {
    super.initState();
    _scrollController.addListener(_onScroll);
  }

  @override
  void dispose() {
    _scrollController.removeListener(_onScroll);
    _scrollController.dispose();
    _searchController.dispose();
    super.dispose();
  }

  /// Requests the next page once the list is scrolled to the end.
  ///
  /// Deliberately reads the service with `listen: false` -- reading it here
  /// would make this callback a build dependency of nothing, and the position
  /// check does not need a rebuild.
  void _onScroll() {
    if (!_scrollController.hasClients) return;
    final position = _scrollController.position;
    if (position.outOfRange) return;
    if (position.pixels < position.maxScrollExtent) return;

    final service = context.read<MyContractsListService>();
    if (service.hasMore && !service.nextPageLoading) {
      service.fetchNextPage();
    }
  }

  @override
  Widget build(BuildContext context) {
    final olProvider = Provider.of<MyContractsListService>(context, listen: false);
    return Scaffold(
      appBar: AppBar(title: Text(LocalKeys.myOrder)),
      body: Column(
        children: [
          _ContractsSearch(
            controller: _searchController,
            service: olProvider,
          ),
          _StatusTabs(service: olProvider),
          Expanded(
            child: Consumer<ProfileInfoService>(
              builder: (context, pi, child) {
                return pi.profileInfoModel.data == null
                    ? Column(
                        children: [
                          16.toHeight,
                          const Expanded(child: AccountSkeleton()),
                          16.toHeight,
                        ],
                      )
                    : CustomRefreshIndicator(
                        onRefresh: () => olProvider.fetchOrderList(),
                        child: CustomFutureWidget(
                          function: olProvider.shouldAutoFetch
                              ? () => olProvider.fetchOrderList()
                              : null,
                          shimmer: const MyContractsSkeleton(),
                          child: Consumer<MyContractsListService>(
                            builder: (context, moProvider, child) {
                              if (moProvider.orderList.isEmpty) {
                                return EmptyWidget(title: LocalKeys.noOrderYet);
                              }

                              // A trailing spinner row only when the server
                              // actually has another page.
                              final showSpinner = moProvider.hasMore &&
                                  !moProvider.nexLoadingFailed;
                              final itemCount = moProvider.orderList.length +
                                  (showSpinner ? 1 : 0);

                              return Scrollbar(
                                controller: _scrollController,
                                child: ListView.separated(
                                  controller: _scrollController,
                                  physics: const AlwaysScrollableScrollPhysics(),
                                  padding: const EdgeInsets.all(20),
                                  itemBuilder: (context, index) {
                                    if (index >= moProvider.orderList.length) {
                                      return const CustomPreloader();
                                    }
                                    return MyContractCard(
                                      contract: moProvider.orderList[index],
                                    );
                                  },
                                  separatorBuilder: (context, index) => 16.toHeight,
                                  itemCount: itemCount,
                                ),
                              );
                            },
                          ),
                        ),
                      );
              },
            ),
          ),
        ],
      ),
    );
  }
}

/// Search box for `?search=` (project title).
class _ContractsSearch extends StatelessWidget {
  final TextEditingController controller;
  final MyContractsListService service;

  const _ContractsSearch({required this.controller, required this.service});

  @override
  Widget build(BuildContext context) {
    return Container(
      color: context.dProvider.whiteColor,
      padding: const EdgeInsets.fromLTRB(20, 12, 20, 0),
      child: TextField(
        controller: controller,
        textInputAction: TextInputAction.search,
        onSubmitted: (value) => service.setSearch(value.trim()),
        decoration: InputDecoration(
          hintText: LocalKeys.search,
          isDense: true,
          prefixIcon: const Icon(Icons.search, size: 20),
          suffixIcon: ValueListenableBuilder<TextEditingValue>(
            valueListenable: controller,
            builder: (context, value, child) => value.text.isEmpty
                ? const SizedBox.shrink()
                : IconButton(
                    icon: const Icon(Icons.close, size: 18),
                    onPressed: () {
                      controller.clear();
                      service.setSearch('');
                    },
                  ),
          ),
          border: OutlineInputBorder(
            borderRadius: BorderRadius.circular(8),
          ),
          contentPadding: const EdgeInsets.symmetric(vertical: 10),
        ),
      ),
    );
  }
}

/// All / Active / Submitted / Completed / Disputed tab row.
class _StatusTabs extends StatelessWidget {
  final MyContractsListService service;
  const _StatusTabs({required this.service});

  @override
  Widget build(BuildContext context) {
    final tabs = <String?, String>{
      null: LocalKeys.all,
      Contract.statusActive: LocalKeys.active,
      Contract.statusSubmitted: LocalKeys.submitted,
      Contract.statusCompleted: LocalKeys.completed,
      Contract.statusDisputed: LocalKeys.disputed,
    };
    return Container(
      width: double.infinity,
      color: context.dProvider.whiteColor,
      padding: const EdgeInsets.fromLTRB(20, 12, 20, 12),
      child: SingleChildScrollView(
        scrollDirection: Axis.horizontal,
        child: Row(
          children: [
            for (final entry in tabs.entries) ...[
              ChoiceChip(
                label: Text(entry.value),
                selected: service.status == entry.key,
                onSelected: (_) => service.setStatus(entry.key),
                selectedColor: context.dProvider.primaryColor,
                backgroundColor: context.dProvider.black9,
                labelStyle: context.titleSmall?.copyWith(
                  color: service.status == entry.key
                      ? Colors.white
                      : context.dProvider.black5,
                ),
                showCheckmark: false,
                visualDensity: VisualDensity.compact,
              ),
              if (entry.key != tabs.keys.last) 8.toWidth,
            ],
          ],
        ),
      ),
    );
  }
}