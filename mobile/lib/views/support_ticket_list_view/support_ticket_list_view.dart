import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../../helper/extension/context_extension.dart';
import '../../helper/local_keys.g.dart';
import '../../services/ticket_list_service.dart';
import '../../utils/components/custom_button.dart';
import '../../utils/components/custom_future_widget.dart';
import '../../utils/components/custom_preloader.dart';
import '../../utils/components/custom_refresh_indicator.dart';
import '../../utils/components/empty_spacer_helper.dart';
import '../../utils/components/empty_widget.dart';
import '../../utils/components/navigation_pop_icon.dart';
import '../../view_models/support_tickets_view_model/support_tickets_view_model.dart';
import '../../views/support_ticket_create_view/support_ticket_create_view.dart';
import '../../views/support_ticket_list_view/components/ticket_tile.dart';

import 'components/ticket_list_skeleton.dart';

class SupportTicketListView extends StatefulWidget {
  static const routeName = 'support_ticket_list_view';
  const SupportTicketListView({super.key});

  @override
  State<SupportTicketListView> createState() => _SupportTicketListViewState();
}

class _SupportTicketListViewState extends State<SupportTicketListView> {
  final stm = SupportTicketViewModel.instance;

  @override
  void initState() {
    super.initState();
    // Was added inside build(), which registered a fresh listener on every
    // rebuild and never removed the previous ones.
    stm.scrollController.addListener(_onScroll);
  }

  @override
  void dispose() {
    stm.scrollController.removeListener(_onScroll);
    super.dispose();
  }

  void _onScroll() {
    if (!mounted) return;
    stm.tryToLoadMore(context);
  }

  @override
  Widget build(BuildContext context) {
    final tlProvider = Provider.of<TicketListService>(context, listen: false);
    return Scaffold(
      appBar: AppBar(
        leading: const NavigationPopIcon(),
        title: Text(LocalKeys.supportTicket),
      ),
      body: CustomRefreshIndicator(
        onRefresh: () async {
          await tlProvider.fetchTicketList();
        },
        child: CustomFutureWidget(
            function: tlProvider.shouldAutoFetch ? tlProvider.fetchTicketList : null,
            shimmer: const TicketListSkeleton(),
            child: Consumer<TicketListService>(builder: (context, tl, child) {
              if (tl.ticketList.isEmpty) {
                return EmptyWidget(title: LocalKeys.noTicketFound);
              }
              final showFooter = tl.hasMore && !tl.nexLoadingFailed;
              return Scrollbar(
                controller: stm.scrollController,
                child: ListView.separated(
                  controller: stm.scrollController,
                  physics: const AlwaysScrollableScrollPhysics(),
                  padding: const EdgeInsets.all(20),
                  itemBuilder: (context, index) {
                    if (showFooter && index == tl.ticketList.length) {
                      return const CustomPreloader();
                    }
                    return TicketTile(ticket: tl.ticketList[index]);
                  },
                  separatorBuilder: (context, index) => EmptySpaceHelper.emptyHeight(12),
                  itemCount: tl.ticketList.length + (showFooter ? 1 : 0),
                ),
              );
            })),
      ),
      // `SafeArea` keeps the button clear of the system navigation bar; the app
      // is edge-to-edge, so its lower half would otherwise sit underneath.
      bottomNavigationBar: SafeArea(
        top: false,
        child: Padding(
          padding: const EdgeInsets.all(20),
          child: CustomButton(
              onPressed: () {
                context.toNamed(SupportTicketCreateView.routeName);
              },
              btText: LocalKeys.createTicket,
              isLoading: false),
        ),
      ),
    );
  }
}
