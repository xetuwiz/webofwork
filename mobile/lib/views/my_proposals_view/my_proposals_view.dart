import 'package:flutter/material.dart';
import 'package:provider/provider.dart';
import 'package:webofwork/helper/extension/context_extension.dart';
import 'package:webofwork/helper/extension/datetime_extension.dart';
import 'package:webofwork/helper/extension/int_extension.dart';
import 'package:webofwork/helper/extension/string_extension.dart';
import 'package:webofwork/helper/extension/widget_extension.dart';
import 'package:webofwork/helper/local_keys.g.dart';
import 'package:webofwork/models/my_proposals_model.dart';
import 'package:webofwork/services/my_proposals_service.dart';
import 'package:webofwork/utils/components/alerts.dart';
import 'package:webofwork/utils/components/custom_future_widget.dart';
import 'package:webofwork/utils/components/custom_preloader.dart';
import 'package:webofwork/utils/components/custom_refresh_indicator.dart';
import 'package:webofwork/utils/components/empty_widget.dart';
import 'package:webofwork/views/my_proposals_view/components/my_proposal_card.dart';
import 'package:webofwork/views/my_proposals_view/components/my_proposal_skeleton.dart';
import 'package:webofwork/utils/components/navigation_pop_icon.dart';

/// The freelancer's submitted proposals.
///
/// Now parses the real `GET /api/projects/my-proposals` envelope
/// (`{count, next, previous, results}`) instead of the `my_proposals` key the
/// server never sends, and follows the server's own relative `next` query
/// string. Before this the request 404'd (the URL pointed at
/// `/api/proposals/`, which is not a route) *and* the envelope lookup missed,
/// so the list was empty no matter which of the two was fixed.
///
/// A `StatefulWidget` now: the scroll listener used to be registered inside
/// `build`, attaching another listener on every rebuild.
class MyProposalsView extends StatefulWidget {
  static const routeName = "my_proposals_view";
  const MyProposalsView({super.key});

  @override
  State<MyProposalsView> createState() => _MyProposalsViewState();
}

class _MyProposalsViewState extends State<MyProposalsView> {
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
    if (position.pixels >= position.maxScrollExtent && !position.outOfRange) {
      final service = context.read<MyProposalsService>();
      if (service.hasMore && !service.nextPageLoading && !service.nexLoadingFailed) {
        service.fetchNextPage();
      }
    }
  }

  /// `DELETE /api/proposals/{id}` -> 204.
  ///
  /// The row is dropped from the local list only on a 204. A 400 (the proposal
  /// is no longer `Pending`) or a 403 leaves it in place, because the server is
  /// telling us our cached copy is stale rather than that the action worked.
  Future<void> _withdraw(MyProposal proposal) async {
    final service = context.read<MyProposalsService>();
    final messenger = ScaffoldMessenger.of(context);
    final errorText = LocalKeys.errorOccurred;

    final ok = await service.withdraw(proposal.id);
    if (!ok) {
      messenger.showSnackBar(SnackBar(content: Text(errorText)));
      // The cached row is suspect, so re-read server truth rather than leaving
      // an action the server has already refused on screen.
      await service.fetchOrderList();
      return;
    }
    messenger.showSnackBar(
      SnackBar(content: Text(LocalKeys.proposalWithdrawn)),
    );
  }

  /// Full proposal details in a sheet.
  ///
  /// The list card truncates the cover letter to four lines; this renders the
  /// whole thing plus the client's name (the server started sending
  /// `client_name` so this screen is not just an id).
  void _openDetails(MyProposal proposal) {
    showModalBottomSheet(
      context: context,
      backgroundColor: Colors.transparent,
      isScrollControlled: true,
      builder: (sheetContext) => _ProposalDetailsSheet(proposal: proposal),
    );
  }

  @override
  Widget build(BuildContext context) {
    final service = context.watch<MyProposalsService>();
    return Scaffold(
      appBar: AppBar(
        // Pushed from the profile menu and from a notification tile.
        leading: const NavigationPopIcon(),
        title: Text(LocalKeys.myProposals),
      ),
      body: Column(
        children: [
          // Status tabs mirror the server's `?status=` filter and the web
          // client's proposals filter chips. Withdrawn proposals only appear
          // under All (there is no "Withdrawn" tab on the web either).
          _StatusTabs(service: service),
          Expanded(
            child: CustomRefreshIndicator(
              onRefresh: service.fetchOrderList,
              child: CustomFutureWidget(
                function: service.shouldAutoFetch
                    ? () => service.fetchOrderList()
                    : null,
                shimmer: const MyProposalsSkeleton(),
                child: service.isEmpty
                    ? ListView(
                        // A scrollable child, because `CustomRefreshIndicator`
                        // needs one to pull on.
                        children: [
                          SizedBox(height: MediaQuery.of(context).size.height * 0.6),
                          EmptyWidget(title: LocalKeys.noProposalYet),
                        ],
                      )
                    : ListView.separated(
                        controller: _scrollController,
                        physics: const AlwaysScrollableScrollPhysics(),
                        padding: const EdgeInsets.all(20),
                        itemBuilder: (context, index) {
                          if (service.hasMore && index == service.proposalList.length) {
                            return const Padding(
                              padding: EdgeInsets.symmetric(vertical: 20),
                              child: Center(child: CustomPreloader()),
                            );
                          }
                          final proposal = service.proposalList[index];
                          return Column(
                            crossAxisAlignment: CrossAxisAlignment.stretch,
                            children: [
                              MyProposalCard(proposal: proposal),
                              Row(
                                mainAxisAlignment: MainAxisAlignment.end,
                                children: [
                                  TextButton.icon(
                                    onPressed: () => _openDetails(proposal),
                                    icon: const Icon(Icons.visibility_outlined, size: 18),
                                    label: Text(
                                      LocalKeys.viewDetails,
                                      style: context.titleSmall?.copyWith(
                                        color: context.dProvider.primaryColor,
                                      ),
                                    ),
                                  ),
                                  if (proposal.canWithdraw)
                                    TextButton.icon(
                                      onPressed: () => Alerts().confirmationAlert(
                                        context: context,
                                        title: LocalKeys.withdraw,
                                        description: LocalKeys.withdrawProposalConfirmation,
                                        buttonText: LocalKeys.withdraw,
                                        buttonColor: context.dProvider.warningColor,
                                        onConfirm: () => _withdraw(proposal),
                                      ),
                                      icon: const Icon(Icons.close, size: 18),
                                      label: Text(
                                        LocalKeys.withdraw,
                                        style: context.titleSmall?.copyWith(
                                          color: context.dProvider.warningColor,
                                        ),
                                      ),
                                    ),
                                ],
                              ).hp20,
                            ],
                          );
                        },
                        separatorBuilder: (context, index) => 16.toHeight,
                        itemCount: service.proposalList.length + (service.hasMore ? 1 : 0),
                      ),
              ),
            ),
          ),
        ],
      ),
    );
  }
}

/// All / Pending / Accepted / Rejected tab row.
class _StatusTabs extends StatelessWidget {
  final MyProposalsService service;
  const _StatusTabs({required this.service});

  @override
  Widget build(BuildContext context) {
    final tabs = <String?, String>{
      null: LocalKeys.all,
      MyProposal.statusPending: LocalKeys.pending,
      MyProposal.statusAccepted: LocalKeys.accepted,
      MyProposal.statusRejected: LocalKeys.rejected,
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

/// Bottom sheet with the full proposal.
class _ProposalDetailsSheet extends StatelessWidget {
  final MyProposal proposal;
  const _ProposalDetailsSheet({required this.proposal});

  @override
  Widget build(BuildContext context) {
    return FractionallySizedBox(
      heightFactor: 0.85,
      child: Container(
        decoration: BoxDecoration(
          borderRadius: const BorderRadius.vertical(top: Radius.circular(20)),
          color: context.dProvider.whiteColor,
        ),
        padding: const EdgeInsets.fromLTRB(20, 12, 20, 20),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Align(
              alignment: Alignment.center,
              child: Container(
                height: 4,
                width: 48,
                margin: const EdgeInsets.all(8),
                decoration: BoxDecoration(
                  borderRadius: BorderRadius.circular(12),
                  color: context.dProvider.black7,
                ),
              ),
            ),
            Text(
              proposal.projectTitle.isNotEmpty
                  ? proposal.projectTitle
                  : '#${proposal.id}',
              style: context.titleLarge?.bold6,
            ),
            8.toHeight,
            if (proposal.clientName.isNotEmpty)
              Text(
                'Client: ${proposal.clientName}',
                style: context.titleSmall?.copyWith(color: context.dProvider.black5),
              ),
            6.toHeight,
            _Row(label: LocalKeys.offerPrice,
                value: proposal.bidAmount.toString().cur),
            if (proposal.deliveryDays > 0)
              _Row(
                  label: LocalKeys.deliveryTime,
                  value: '${proposal.deliveryDays} ${LocalKeys.days}'),
            if (proposal.createdAt != null)
              _Row(label: 'Submitted', value: proposal.createdAt!.friendlyDateTime),
            16.toHeight,
            const Divider(height: 1, thickness: 1),
            16.toHeight,
            Text(LocalKeys.yourCoverLatter, style: context.titleMedium?.bold6),
            8.toHeight,
            Expanded(
              child: SingleChildScrollView(
                child: Text(
                  proposal.coverLetter.isEmpty
                      ? 'No cover letter.'
                      : proposal.coverLetter,
                  style: context.bodyMedium?.copyWith(color: context.dProvider.black4),
                ),
              ),
            ),
          ],
        ),
      ),
    );
  }
}

class _Row extends StatelessWidget {
  final String label;
  final String value;
  const _Row({required this.label, required this.value});

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.symmetric(vertical: 6),
      child: Row(
        children: [
          Expanded(
            flex: 2,
            child: Text(
              label,
              style: context.titleSmall?.copyWith(color: context.dProvider.black5),
            ),
          ),
          Expanded(
            flex: 3,
            child: Text(
              value,
              maxLines: 2,
              overflow: TextOverflow.ellipsis,
              textAlign: TextAlign.end,
              style: context.titleSmall?.bold6,
            ),
          ),
        ],
      ),
    );
  }
}
