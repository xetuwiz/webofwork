import 'package:flutter/material.dart';
import 'package:provider/provider.dart';
import 'package:webofwork/helper/extension/int_extension.dart';
import 'package:webofwork/helper/local_keys.g.dart';
import 'package:webofwork/services/my_offers_service.dart';
import 'package:webofwork/utils/components/custom_preloader.dart';
import 'package:webofwork/utils/components/custom_refresh_indicator.dart';
import 'package:webofwork/utils/components/empty_widget.dart';
import 'package:webofwork/views/my_offers_view/components/my_offers_skeleton.dart';
import 'package:webofwork/views/my_contracts_view/components/my_contract_card.dart';

import '../../utils/components/custom_future_widget.dart';
import 'package:webofwork/utils/components/navigation_pop_icon.dart';

/// The freelancer's inbound offers.
///
/// Now renders [MyContractCard] against a parsed [Contract] rather than a private
/// `Offer` model, so this screen and the contracts list read the same fields from
/// the same source.
///
/// Two bugs fixed here beyond the payload:
///  * The scroll listener was registered inside `build`
///    (`mom.scrollController.addListener(...)` on every rebuild), so a rebuild
///    attached another listener. It is a `StatefulWidget` now: added in
///    `initState`, removed in `dispose`.
///  * The customer name was built as `firstName + firstName` instead of
///    `firstName + lastName`, and the avatar path was assembled from a
///    `profile_image_path` the API does not send. `ContractResponse` carries
///    `client_name` and no photo, so `MyContractCardInfos` shows the name with an
///    initials avatar -- the same as the contracts list.
class MyOffersView extends StatefulWidget {
  static const routeName = "my_offers_view";
  const MyOffersView({super.key});

  @override
  State<MyOffersView> createState() => _MyOffersViewState();
}

class _MyOffersViewState extends State<MyOffersView> {
  final _scrollController = ScrollController();

  @override
  void initState() {
    super.initState();
    _scrollController.addListener(_onScroll);
  }

  @override
  void dispose() {
    _scrollController.removeListener(_onScroll);
    _scrollController.dispose();
    super.dispose();
  }

  void _onScroll() {
    if (!_scrollController.hasClients) return;
    final position = _scrollController.position;
    // `maxScrollExtent` is 0 when the list is short, so the position is always
    // "at the end" -- the explicit out-of-range check keeps that from firing a
    // fetch on every scroll event of a non-paging list.
    if (position.pixels >= position.maxScrollExtent && !position.outOfRange) {
      final service = context.read<MyOffersService>();
      if (service.hasMore && !service.isLoading) {
        service.fetchOrderList();
      }
    }
  }

  @override
  Widget build(BuildContext context) {
    final service = context.watch<MyOffersService>();
    return Scaffold(
      appBar: AppBar(
        // Pushed from the profile menu, so it needs the app's back affordance.
        // Without this it falls back to the default Material arrow while every
        // sibling screen shows `NavigationPopIcon`.
        leading: const NavigationPopIcon(),
        title: Text(LocalKeys.myOffers),
      ),
      body: CustomRefreshIndicator(
        onRefresh: service.fetchOrderList,
        child: CustomFutureWidget(
          // Callback, not an already-created Future: invoking it eagerly here
          // would start the fetch during build(), and the service's first
          // notifyListeners() would mark this Consumer dirty mid-build.
          function: service.shouldAutoFetch ? service.fetchOrderList : null,
          shimmer: const MyOffersSkeleton(),
          child: service.isEmpty
              ? ListView(
                  // A scrollable child, because `CustomRefreshIndicator` needs
                  // one to pull on.
                  children: [
                    SizedBox(height: MediaQuery.of(context).size.height * 0.6),
                    EmptyWidget(title: LocalKeys.noOrderYet),
                  ],
                )
              : ListView.separated(
                  controller: _scrollController,
                  physics: const AlwaysScrollableScrollPhysics(),
                  padding: const EdgeInsets.all(20),
                  itemBuilder: (context, index) {
                    if (service.hasMore && index == service.offerList.length) {
                      return const Padding(
                        padding: EdgeInsets.symmetric(vertical: 20),
                        child: Center(child: CustomPreloader()),
                      );
                    }
                    return MyContractCard(contract: service.offerList[index]);
                  },
                  separatorBuilder: (context, index) => 12.toHeight,
                  itemCount: service.offerList.length + (service.hasMore ? 1 : 0),
                ),
        ),
      ),
    );
  }
}
