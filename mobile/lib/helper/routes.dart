import 'package:webofwork/views/fixed_price_project_details_view/fixed_price_project_details_view.dart';
import 'package:webofwork/views/intro_view/intro_view.dart';
import 'package:webofwork/views/my_offers_view/my_offers_view.dart';
import 'package:webofwork/views/my_contract_details_view/my_contract_details_view.dart';
import 'package:webofwork/views/my_contracts_view/my_contracts_view.dart';
import 'package:webofwork/views/profile_details_view/profile_details_view.dart';
import 'package:webofwork/views/profile_settings_view/profile_settings_view.dart';
import 'package:webofwork/views/payment_settings_view/payment_settings_view.dart';
import 'package:webofwork/views/notification_preferences_view/notification_preferences_view.dart';
import 'package:webofwork/views/support_ticket_list_view/support_ticket_list_view.dart';

import '../views/my_proposals_view/my_proposals_view.dart';
import '../views/onboarding_view/onboarding_view.dart';
import '../views/chat_list_view/chat_list_view.dart';
import '../views/notifications_list_view/notifications_list_view.dart';
import '../views/support_ticket_create_view/support_ticket_create_view.dart';
import '../views/invitations_list_view/invitations_list_view.dart';
import '/views/change_password_view/change_password_view.dart';
import '/views/conversation_view/conversation_view.dart';
import '/views/profile_edit_view/identity_info_edit_view.dart';
import '/views/profile_edit_view/profile_edit_view.dart';
import '/views/sign_in_view/sign_in_view.dart';
import '/views/sign_up_view/sign_up_view.dart';
import '/views/forgot_password_view/forgot_password_view.dart';

class Routes {
  static var routes = {
    SignInView.routeName: (context) => const SignInView(),
    SignUpView.routeName: (context) => const SignUpView(),
    OnboardingView.routeName: (context) => const OnboardingView(),
    ConversationView.routeName: (context) => const ConversationView(),
    ChangePasswordView.routeName: (context) => const ChangePasswordView(),
    IntroView.routeName: (context) => const IntroView(),
    ProfileEditView.routeName: (context) => const ProfileEditView(),
    IdentityInfoEditView.routeName: (context) => const IdentityInfoEditView(),
    MyContractsView.routeName: (context) => const MyContractsView(),
    MyContractDetailsView.routeName: (context) => const MyContractDetailsView(),
    ProfileSettingsView.routeName: (context) => const ProfileSettingsView(),
    PaymentSettingsView.routeName: (context) => const PaymentSettingsView(),
    NotificationPreferencesView.routeName: (context) =>
        const NotificationPreferencesView(),
    ProfileDetailsView.routeName: (context) => const ProfileDetailsView(),
    SupportTicketListView.routeName: (context) => const SupportTicketListView(),
    SupportTicketCreateView.routeName: (context) => const SupportTicketCreateView(),
    FixedPriceProjectDetailsView.routeName: (context) => const FixedPriceProjectDetailsView(),
    MyOffersView.routeName: (context) => const MyOffersView(),
    MyProposalsView.routeName: (context) => const MyProposalsView(),
    ChatListView.routeName: (context) => const ChatListView(),
    NotificationListView.routeName: (context) => const NotificationListView(),
    InvitationsListView.routeName: (context) => const InvitationsListView(),
    // Password reset is a magic link, so the app's only step is "request it".
    ForgotPasswordView.routeName: (context) => const ForgotPasswordView(),
  };
}
