import 'package:flutter/material.dart';
import 'package:provider/provider.dart';
import 'package:webofwork/helper/extension/context_extension.dart';
import 'package:webofwork/services/message_notification_count_service.dart';
import 'package:webofwork/services/profile_info_service.dart';

import 'package:webofwork/views/chat_list_view/chat_list_view.dart';
import 'package:webofwork/views/notifications_list_view/notifications_list_view.dart';
import 'components/todo_list_widget.dart';
import 'components/project_recommendations_widget.dart';
import 'components/active_contracts_widget.dart';
import 'components/invitations_widget.dart';
import 'package:webofwork/views/invitations_list_view/invitations_list_view.dart';
import 'package:webofwork/view_models/onboarding_view_model/onboarding_view_model.dart';
import 'package:webofwork/helper/local_keys.g.dart';

class DashboardView extends StatelessWidget {
  static const routeName = 'dashboard_view';
  const DashboardView({super.key});

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: context.dProvider.whiteColor,
      appBar: AppBar(
        // Was a hardcoded 'Dashboard' string, so this screen ignored the app's
        // locale. A tab root correctly has no back button.
        title: Text(LocalKeys.dashboard),
        centerTitle: false,
        actions: [
          IconButton(
            icon: const Icon(Icons.chat_bubble_outline),
            onPressed: () {
              context.toNamed(ChatListView.routeName);
            },
          ),
          // The unread count is pushed in from the notifications list
          // envelope (`data.unread_count`), so the bell shows a live number
          // while the user is on the dashboard.
          Consumer<MessageNotificationCountService>(
            builder: (context, counts, child) {
              final n = counts.notificationCount;
              return IconButton(
                icon: _BadgedBell(count: n),
                onPressed: () {
                  context.toNamed(NotificationListView.routeName);
                },
              );
            },
          ),
        ],
      ),
      body: Consumer<ProfileInfoService>(
        builder: (context, pi, child) {
          // Sourced from /api/auth/me/ via ProfileInfoService. Empty until that
          // lands, in which case the banner simply omits the name line rather
          // than showing a placeholder like "Freelancer".
          final profileName = pi.profileInfoModel.data?.firstName ?? '';
          return ListView(
            padding: const EdgeInsets.all(16.0),
            children: [
              _buildWelcomeBanner(context, profileName),
              const SizedBox(height: 24),
              _buildSectionTitle(
                context,
                'Pending Invitations',
                actionLabel: LocalKeys.seeAll,
                onAction: () {
                  context.toNamed(InvitationsListView.routeName);
                },
              ),
              const SizedBox(height: 12),
              const InvitationsWidget(),
              const SizedBox(height: 24),
              _buildSectionTitle(context, 'Active Contracts'),
              const SizedBox(height: 12),
              const ActiveContractsWidget(),
              const SizedBox(height: 24),
              _buildSectionTitle(context, 'Todo List'),
              const SizedBox(height: 12),
              const TodoListWidget(),
              const SizedBox(height: 24),
              _buildSectionTitle(context, 'Recommended for You'),
              const SizedBox(height: 12),
              const ProjectRecommendationsWidget(),
              const SizedBox(height: 48), // Padding at bottom
            ],
          );
        },
      ),
    );
  }

  Widget _buildWelcomeBanner(BuildContext context, String name) {
    return Container(
      padding: const EdgeInsets.all(20),
      decoration: BoxDecoration(
        color: context.dProvider.primaryColor,
        borderRadius: BorderRadius.circular(12),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(
            'Welcome back,',
            style: context.titleMedium?.copyWith(
              color: Colors.white70,
            ),
          ),
          if (name.isNotEmpty) ...[
            const SizedBox(height: 4),
            Text(
              name,
              maxLines: 1,
              overflow: TextOverflow.ellipsis,
              style: context.headlineSmall?.copyWith(
                color: Colors.white,
                fontWeight: FontWeight.bold,
              ),
            ),
          ],
          const SizedBox(height: 16),
          Row(
            children: [
              Expanded(
                child: ElevatedButton(
                  style: ElevatedButton.styleFrom(
                    backgroundColor: Colors.white,
                    foregroundColor: context.dProvider.primaryColor,
                    shape: RoundedRectangleBorder(
                      borderRadius: BorderRadius.circular(8),
                    ),
                  ),
                  onPressed: () {
                    // "Find Work" lives in the shell's tab strip (tab 1 =
                    // `HomeView`), so this switches tabs rather than pushing a
                    // route. Pushing a second `HomeView` on top of the shell
                    // would give the user a Find Projects screen with no bottom nav
                    // and no way back to the rest of the app without the system
                    // back gesture.
                    //
                    // `setNavIndex` is a no-op when the tab is already selected,
                    // so tapping this from the Find Projects tab is harmless.
                    OnboardingViewModel.instance.setNavIndex(1);
                  },
                  child: const Text('Find Work'),
                ),
              ),
              const SizedBox(width: 12),
              Expanded(
                child: OutlinedButton(
                  style: OutlinedButton.styleFrom(
                    foregroundColor: Colors.white,
                    side: const BorderSide(color: Colors.white70),
                    shape: RoundedRectangleBorder(
                      borderRadius: BorderRadius.circular(8),
                    ),
                  ),
                  onPressed: () {
                    context.toNamed('profile_details_view');
                  },
                  child: const Text('View Profile'),
                ),
              ),
            ],
          ),
        ],
      ),
    );
  }

  Widget _buildSectionTitle(BuildContext context, String title,
      {String? actionLabel, VoidCallback? onAction}) {
    return Row(
      mainAxisAlignment: MainAxisAlignment.spaceBetween,
      children: [
        Text(
          title,
          style: context.titleLarge?.copyWith(
            fontWeight: FontWeight.bold,
            color: context.dProvider.black3,
          ),
        ),
        if (actionLabel != null && onAction != null)
          TextButton(
            onPressed: onAction,
            style: TextButton.styleFrom(
              padding: EdgeInsets.zero,
              minimumSize: const Size(0, 0),
              tapTargetSize: MaterialTapTargetSize.shrinkWrap,
            ),
            child: Text(
              actionLabel,
              style: context.titleSmall?.copyWith(
                color: context.dProvider.primaryColor,
                fontWeight: FontWeight.bold,
              ),
            ),
          ),
      ],
    );
  }
}

/// Bell icon with the unread-notification count, or a plain bell when there is
/// nothing unread.
class _BadgedBell extends StatelessWidget {
  final num count;
  const _BadgedBell({required this.count});

  @override
  Widget build(BuildContext context) {
    final badge = count > 0 ? count : null;
    return Stack(
      clipBehavior: Clip.none,
      children: [
        const Icon(Icons.notifications_none),
        if (badge != null)
          Positioned(
            right: -6,
            top: -6,
            child: Container(
              padding: const EdgeInsets.symmetric(horizontal: 5, vertical: 1),
              decoration: BoxDecoration(
                color: context.dProvider.warningColor,
                borderRadius: BorderRadius.circular(10),
              ),
              constraints: const BoxConstraints(minWidth: 16),
              child: Text(
                badge > 99 ? '99+' : '$badge',
                textAlign: TextAlign.center,
                style: const TextStyle(
                  color: Colors.white,
                  fontSize: 10,
                  fontWeight: FontWeight.bold,
                  height: 1.4,
                ),
              ),
            ),
          ),
      ],
    );
  }
}
