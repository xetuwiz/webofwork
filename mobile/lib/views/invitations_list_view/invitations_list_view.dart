import 'package:flutter/material.dart';
import 'package:webofwork/utils/components/app_card.dart';
import 'package:provider/provider.dart';
import 'package:webofwork/helper/extension/context_extension.dart';
import 'package:webofwork/services/invitation_service.dart';
import 'package:webofwork/utils/components/navigation_pop_icon.dart';
import 'package:webofwork/helper/local_keys.g.dart';
import 'package:webofwork/views/dashboard_view/components/match_reason_card.dart';

class InvitationsListView extends StatefulWidget {
  static const routeName = 'invitations_list_view';
  const InvitationsListView({super.key});

  @override
  State<InvitationsListView> createState() => _InvitationsListViewState();
}

class _InvitationsListViewState extends State<InvitationsListView>
    with SingleTickerProviderStateMixin {
  late TabController _tabController;
  final GlobalKey<RefreshIndicatorState> _refreshKey = GlobalKey<RefreshIndicatorState>();

  /// One filter per tab, matched against [InvitationStatus.normalize]d values.
  ///
  /// The previous tabs filtered on `['Pending', '0']`, `['Accepted', '1']` and
  /// `['Hired', '4']`. `status` comes off the wire as `pending` / `accepted` /
  /// `declined` / `cancelled`, so **all three tabs were permanently empty** --
  /// and `Hired` is not a state this API has at all.
  /// `InvitationStatus.all` is `pending, accepted, declined, cancelled`.
  static const List<List<String>?> _tabFilters = [
    null, // All
    [InvitationStatus.pending],
    [InvitationStatus.accepted],
    [InvitationStatus.declined],
  ];

  @override
  void initState() {
    super.initState();
    _tabController = TabController(length: _tabFilters.length, vsync: this);
    WidgetsBinding.instance.addPostFrameCallback((_) {
      if (mounted) context.read<InvitationService>().fetchInvitations();
    });
  }

  @override
  void dispose() {
    _tabController.dispose();
    super.dispose();
  }

  Future<void> _refresh() async {
    final service = context.read<InvitationService>();
    await service.fetchInvitations();
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(
        leading: const NavigationPopIcon(),
        // Was a hardcoded 'Invitations' string, so this one screen ignored the
        // app's locale entirely.
        title: Text(LocalKeys.invitations),
        bottom: TabBar(
          controller: _tabController,
          isScrollable: true,
          labelColor: context.dProvider.primaryColor,
          unselectedLabelColor: context.dProvider.black5,
          indicatorColor: context.dProvider.primaryColor,
          tabs: const [
            Tab(text: 'All'),
            Tab(text: 'Pending'),
            Tab(text: 'Accepted'),
            Tab(text: 'Declined'),
          ],
        ),
      ),
      body: Consumer<InvitationService>(
        builder: (context, invService, child) {
          if (invService.isLoading && invService.invitations.isEmpty) {
            return const Center(child: CircularProgressIndicator());
          }

          return TabBarView(
            controller: _tabController,
            children: [
              for (final filter in _tabFilters) _buildList(context, invService, filter),
            ],
          );
        },
      ),
    );
  }

  Widget _buildList(
      BuildContext context, InvitationService invService, List<String>? statuses) {
    final list = invService.invitations
        .where((i) => statuses == null || statuses.contains(i.status))
        .toList();

    return RefreshIndicator(
      key: _refreshKey,
      onRefresh: _refresh,
      child: list.isEmpty
          ? _EmptyState(
              isAll: statuses == null,
              // An empty *filtered* tab is not an error, so keep the refresh
              // path available on all of them.
              child: ListView(
                physics: const AlwaysScrollableScrollPhysics(),
                children: [
                  SizedBox(height: context.height * 0.25),
                  Center(
                    child: Text(
                      statuses == null
                          ? 'No invitations yet'
                          : 'No ${InvitationStatus.label(statuses.first).toLowerCase()} invitations',
                      style:
                          context.titleMedium?.copyWith(color: context.dProvider.black5),
                    ),
                  ),
                ],
              ),
            )
          : ListView.builder(
              padding: const EdgeInsets.all(16),
              itemCount: list.length,
              itemBuilder: (context, index) => _InvitationCard(invitation: list[index]),
            ),
    );
  }
}

class _EmptyState extends StatelessWidget {
  final bool isAll;
  final Widget child;
  const _EmptyState({required this.isAll, required this.child});

  @override
  Widget build(BuildContext context) => child;
}

class _InvitationCard extends StatelessWidget {
  final Invitation invitation;
  const _InvitationCard({required this.invitation});

  @override
  Widget build(BuildContext context) {
    final service = context.read<InvitationService>();
    final isPending = invitation.isActionable;

    // The server's own enum, not an int: `declined` and `cancelled` are
    // terminal and re-PATCHing them is rejected.
    final pendingColour = isPending
        ? context.dProvider.warningColor
        : invitation.status == InvitationStatus.accepted
            ? context.dProvider.greenColor
            : context.dProvider.black5;

    return Container(
      margin: const EdgeInsets.only(bottom: 16),
      padding: const EdgeInsets.all(16),
      decoration: BoxDecoration(
        color: context.dProvider.whiteColor,
        borderRadius: BorderRadius.circular(AppRadius.card),
        border: Border.all(color: context.dProvider.black8),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              Expanded(
                child: Text(
                  // `client_name` can legitimately be blank -- the server
                  // projects it from the user record and it is not guaranteed.
                  invitation.clientName.isEmpty ? 'A client' : invitation.clientName,
                  style: context.titleMedium?.copyWith(fontWeight: FontWeight.bold),
                  maxLines: 1,
                  overflow: TextOverflow.ellipsis,
                ),
              ),
              const SizedBox(width: 8),
              Container(
                padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 4),
                decoration: BoxDecoration(
                  color: pendingColour.withValues(alpha: 0.1),
                  borderRadius: BorderRadius.circular(20),
                ),
                child: Text(
                  InvitationStatus.label(invitation.status),
                  style: context.titleSmall?.copyWith(color: pendingColour),
                ),
              ),
            ],
          ),
          const SizedBox(height: 8),
          Text(invitation.projectTitle, style: context.titleMedium),
          if (invitation.packageAmount != null &&
              invitation.packageAmount!.isNotEmpty) ...[
            const SizedBox(height: 6),
            Text(
              // `package_amount` is a quoted decimal on this API.
              '${invitation.packageType ?? 'Package'} · ${invitation.packageAmount}',
              style: context.titleSmall?.copyWith(color: context.dProvider.black5),
            ),
          ],
          if (invitation.message != null && invitation.message!.isNotEmpty) ...[
            const SizedBox(height: 12),
            Text(
              'Message:',
              style: context.titleSmall?.copyWith(fontWeight: FontWeight.bold),
            ),
            Text(
              invitation.message!,
              style: context.titleSmall?.copyWith(color: context.dProvider.black5),
            ),
          ],
          // Expanded by default here, unlike on the dashboard. This is the full
          // screen the freelancer opened in order to decide, and the reasoning
          // is the reason to be here -- hiding it one tap down would make the
          // AI match invisible on the one screen where it matters.
          if (invitation.matchContext != null)
            MatchReasonCard(
              match: invitation.matchContext!,
              initiallyExpanded: true,
            ),
          // Gated on the real `isPending`. The old check was
          // `inv.status == 'Pending' || inv.status == '0'`, which is never true
          // for a server value, so these two buttons were dead code: the
          // freelancer could read invitations but never answer one.
          if (isPending) ...[
            const SizedBox(height: 16),
            Row(
              children: [
                Expanded(
                  child: OutlinedButton(
                    onPressed: () async {
                      final ok = await service.updateStatus(
                          invitation.id, InvitationStatus.declined);
                      if (!ok) service.showLastError();
                    },
                    style: OutlinedButton.styleFrom(
                      foregroundColor: context.dProvider.warningColor,
                      side: BorderSide(color: context.dProvider.warningColor),
                    ),
                    child: const Text('Decline'),
                  ),
                ),
                const SizedBox(width: 12),
                Expanded(
                  child: ElevatedButton(
                    onPressed: () async {
                      final ok = await service.updateStatus(
                          invitation.id, InvitationStatus.accepted);
                      if (!ok) service.showLastError();
                    },
                    style: ElevatedButton.styleFrom(
                      backgroundColor: context.dProvider.primaryColor,
                      foregroundColor: context.dProvider.whiteColor,
                    ),
                    child: const Text('Accept'),
                  ),
                ),
              ],
            )
          ],
        ],
      ),
    );
  }
}
