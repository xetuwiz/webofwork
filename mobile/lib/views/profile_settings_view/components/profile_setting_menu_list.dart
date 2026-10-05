import 'package:flutter/material.dart';
import 'package:webofwork/helper/svg_assets.dart';
import 'package:webofwork/view_models/change_password_view_model/change_password_view_model.dart';
import 'package:webofwork/views/profile_edit_view/profile_edit_view.dart';
import '../../profile_view/components/profile_menu_tile.dart';
import '../../support_ticket_list_view/support_ticket_list_view.dart';
import '/helper/extension/context_extension.dart';
import '/helper/local_keys.g.dart';
import '/views/change_password_view/change_password_view.dart';
import '/views/payment_settings_view/payment_settings_view.dart';
import '/views/notification_preferences_view/notification_preferences_view.dart';

class ProfileSettingMenuList extends StatelessWidget {
  const ProfileSettingMenuList({super.key});

  @override
  Widget build(BuildContext context) {
    return Column(
      children: [
        ProfileMenuTile(
          title: LocalKeys.editProfile,
          svg: SvgAssets.userSquare,
          onPress: () {
            context.toNamed(ProfileEditView.routeName);
          },
        ),
        // Payout destination. Freelancer-only on the server
        // (`RequireFreelancer` on both routes) and this app is freelancer-only,
        // so there is no role gate to mirror here.
        ProfileMenuTile(
          title: LocalKeys.paymentMethods,
          svg: SvgAssets.wallet,
          onPress: () {
            context.toNamed(PaymentSettingsView.routeName);
          },
        ),
        // Channel switches and per-type mutes. Backed by the server's
        // /api/notifications/preferences, which the publish path consults, so a
        // switch here genuinely suppresses delivery rather than just hiding a
        // screen.
        ProfileMenuTile(
          title: LocalKeys.notificationPreferences,
          svg: SvgAssets.notification,
          onPress: () {
            context.toNamed(NotificationPreferencesView.routeName);
          },
        ),
        ProfileMenuTile(
          title: LocalKeys.supportTicket,
          svg: SvgAssets.support,
          onPress: () {
            context.toNamed(SupportTicketListView.routeName);
          },
        ),
        ProfileMenuTile(
          title: LocalKeys.changePassword,
          svg: SvgAssets.lock,
          onPress: () {
            ChangePasswordViewModel.dispose;
            context.toNamed(ChangePasswordView.routeName);
          },
        ),
      ],
    );
  }
}
