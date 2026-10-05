import 'package:flutter/material.dart';
import 'package:provider/provider.dart';
import 'package:webofwork/helper/extension/context_extension.dart';
import 'package:webofwork/services/invitation_service.dart';
import 'package:webofwork/views/invitations_list_view/invitations_list_view.dart';
import 'package:webofwork/views/dashboard_view/components/match_reason_card.dart';

class InvitationsWidget extends StatefulWidget {
  const InvitationsWidget({super.key});

  @override
  State<InvitationsWidget> createState() => _InvitationsWidgetState();
}

class _InvitationsWidgetState extends State<InvitationsWidget> {
  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addPostFrameCallback((_) {
      context.read<InvitationService>().fetchInvitations();
    });
  }

  @override
  Widget build(BuildContext context) {
    return Consumer<InvitationService>(
      builder: (context, invService, child) {
        if (invService.isLoading && invService.invitations.isEmpty) {
          return const Center(child: CircularProgressIndicator());
        }

        // `isPending` is now `status == 'pending'`. The old check was
        // `i.status == 'Pending' || i.status == '0'`, and the server sends
        // snake_case (`pending`/`accepted`/`declined`/`cancelled`), so this
        // comparison was always false and the dashboard reported "No pending
        // invitations" no matter how many were waiting.
        final pending = invService.invitations.where((i) => i.isPending).toList();

        if (pending.isEmpty) {
          return Padding(
            padding: const EdgeInsets.symmetric(vertical: 24),
            child: Center(
              child: Text(
                'No pending invitations',
                style: context.titleSmall?.copyWith(color: context.dProvider.black5),
              ),
            ),
          );
        }

        return SizedBox(
          height: 190,
          child: ListView.builder(
            scrollDirection: Axis.horizontal,
            itemCount: pending.length,
            itemBuilder: (context, index) {
              final inv = pending[index];
              return GestureDetector(
                onTap: () => context.toNamed(InvitationsListView.routeName),
                child: Container(
                  width: 280,
                  margin: const EdgeInsets.only(right: 16),
                  padding: const EdgeInsets.all(16),
                  decoration: BoxDecoration(
                    color: context.dProvider.whiteColor,
                    borderRadius: BorderRadius.circular(8),
                    border: Border.all(color: context.dProvider.black8),
                  ),
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Row(
                        children: [
                          CircleAvatar(
                            radius: 20,
                            backgroundColor:
                                context.dProvider.primaryColor.withValues(alpha: 0.1),
                            child: Icon(Icons.business,
                                color: context.dProvider.primaryColor),
                          ),
                          const SizedBox(width: 12),
                          Expanded(
                            child: Column(
                              crossAxisAlignment: CrossAxisAlignment.start,
                              children: [
                                Text(
                                  inv.clientName,
                                  style: context.titleMedium
                                      ?.copyWith(fontWeight: FontWeight.bold),
                                  maxLines: 1,
                                  overflow: TextOverflow.ellipsis,
                                ),
                                Text(
                                  'Invited you to a project',
                                  style: context.titleSmall
                                      ?.copyWith(color: context.dProvider.black5),
                                ),
                              ],
                            ),
                          ),
                        ],
                      ),
                      const SizedBox(height: 12),
                      Text(
                        inv.projectTitle,
                        style: context.titleMedium,
                        maxLines: 1,
                        overflow: TextOverflow.ellipsis,
                      ),
                      if (inv.message != null && inv.message!.isNotEmpty) ...[
                        const SizedBox(height: 4),
                        Text(
                          inv.message!,
                          style: context.titleSmall
                              ?.copyWith(color: context.dProvider.black5),
                          maxLines: 2,
                          overflow: TextOverflow.ellipsis,
                        ),
                      ],
                      // Shown collapsed, and only for an invitation that
                      // actually carries one. The card is 190px tall and this
                      // row would otherwise push "Tap to review" off the bottom.
                      if (inv.matchContext != null)
                        MatchReasonCard(match: inv.matchContext!),
                      // Tappable: tapping the card is the only way to reach the
                      // list view, which is where Accept/Decline live.
                      const SizedBox(height: 8),
                      Text(
                        'Tap to review',
                        style: context.titleSmall?.copyWith(
                          color: context.dProvider.primaryColor,
                          fontWeight: FontWeight.bold,
                        ),
                      )
                    ],
                  ),
                ),
              );
            },
          ),
        );
      },
    );
  }
}
