import 'package:flutter/material.dart';
import 'package:webofwork/helper/extension/context_extension.dart';
import 'package:webofwork/views/home_drawer_view/home_drawer_view.dart';
import 'package:webofwork/views/my_contracts_view/my_contracts_view.dart';

import '/view_models/onboarding_view_model/onboarding_view_model.dart';
import '/views/profile_view/profile_view.dart';

import '/views/home_view/home_view.dart';
import '/views/chat_list_view/chat_list_view.dart';
import '/views/notifications_list_view/notifications_list_view.dart';
import '/views/my_proposals_view/my_proposals_view.dart';
import '/views/invitations_list_view/invitations_list_view.dart';
import '/views/my_contract_details_view/my_contract_details_view.dart';
import 'package:webofwork/helper/constant_helper.dart';
import 'package:webofwork/services/push_notification_service.dart';
import '../dashboard_view/dashboard_view.dart';
import 'components/notification_realtime_binder.dart';
import 'components/onboarding_bottom_nav.dart';

class OnboardingView extends StatelessWidget {
  static const routeName = 'landing';
  const OnboardingView({super.key});

  @override
  Widget build(BuildContext context) {
    final ov = OnboardingViewModel.instance;

    // A tap on a tray notification that launched the app lands here before any
    // route exists, so `PushNotificationService` stashed it rather than
    // navigating. Replayed now, once the shell is up. Wrapped in a post-frame
    // because navigating during build is the `setState during build` class of
    // crash this app has already been bitten by once.
    WidgetsBinding.instance.addPostFrameCallback((_) {
      final tap = PendingNotificationTap.take();
      if (tap == null) return;
      final nav = navigatorKey.currentState;
      if (nav == null) return;
      // `pushNamed` rather than the `context.toNamed` extension: that extension is
      // on BuildContext, and there is no context here by design.
      //
      // The routes mirror what `NotificationListTile._onTap` does for the same row,
      // so a push and an in-app tap of one notification land in the same place.
      // `contract_id` is passed as a route argument because `MyContractDetailsView`
      // reads it from `ModalRoute.settings.arguments`.
      final relatedId = int.tryParse(tap.data['related_entity_id'] ?? '');
      switch (tap.route) {
        case 'chat':
          nav.pushNamed(ChatListView.routeName);
          break;
        case 'contract':
          if (relatedId != null) {
            nav.pushNamed(MyContractDetailsView.routeName, arguments: relatedId);
          } else {
            nav.pushNamed(NotificationListView.routeName);
          }
          break;
        case 'proposals':
          nav.pushNamed(MyProposalsView.routeName);
          break;
        case 'invitations':
          nav.pushNamed(InvitationsListView.routeName);
          break;
        case 'notifications':
        default:
          nav.pushNamed(NotificationListView.routeName);
      }
    });

    // Tab order:
    //   0 = Dashboard
    //   1 = Home (Find Projects)
    //   2 = Contracts (MyContractsView)
    //   3 = Profile
    final widgets = [
      const DashboardView(),
      const HomeView(),
      const MyContractsView(),
      const ProfileView(),
    ];

    return WillPopScope(
      onWillPop: ov.willPopFunction,
      child: Scaffold(
        key: ov.scaffoldKey,
        drawer: ValueListenableBuilder(
          valueListenable: ov.currentIndex,
          builder: (context, value, child) => value != 1
              ? const SizedBox(width: 0)
              : Drawer(
                  backgroundColor: context.dProvider.black9,
                  surfaceTintColor: context.dProvider.black9,
                  child: const HomeDrawerView(),
                ),
        ),
        body: Stack(
          children: [
            ValueListenableBuilder(
              valueListenable: ov.currentIndex,
              builder: (context, value, child) => widgets[value],
            ),
            // Mounted here because the shell *is* the signed-in lifetime: this
            // opens `/ws/notifications` on both a fresh sign-in and a restored
            // session, and closes it on sign-out. See
            // `NotificationRealtimeBinder` for why it lives here rather than in
            // `SignInService`.
            const NotificationRealtimeBinder(),
          ],
        ),
        bottomNavigationBar: const OnboardingNavBar(),
      ),
    );
  }
}
