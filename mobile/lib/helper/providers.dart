import 'package:provider/provider.dart';
import 'package:provider/single_child_widget.dart';
import 'package:webofwork/services/account_delete_service.dart';
import 'package:webofwork/services/auth/change_password_service.dart';
import 'package:webofwork/services/auth/sign_in_service.dart';
import 'package:webofwork/services/auth/sign_out_service.dart';
import 'package:webofwork/services/chat_list_service.dart';
import 'package:webofwork/services/project_details_service.dart';
import 'package:webofwork/services/project_list_service.dart'; // fixed: was .dart.dart
import 'package:webofwork/services/my_contracts_list_service.dart';
import 'package:webofwork/services/my_proposals_service.dart';
import 'package:webofwork/services/payment_settings_service.dart';
import 'package:webofwork/services/push_notification_service.dart';
import 'package:webofwork/services/notification_preferences_service.dart';
import 'package:webofwork/services/contract_details_service.dart';
import 'package:webofwork/services/profile_edit_service.dart';
import 'package:webofwork/services/profile_info_service.dart';
import 'package:webofwork/services/proposal_service.dart';
import 'package:webofwork/services/support_ticket_create_service.dart';
import 'package:webofwork/services/ticket_list_service.dart';
import 'package:webofwork/services/todo_service.dart';
import 'package:webofwork/services/recommendations_service.dart';
import 'package:webofwork/services/invitation_service.dart';

import '../services/auth/otp_service.dart';
import '../services/category_dropdown_service.dart';
import '../services/skill_dropdown_service.dart';
import '../services/conversation_service.dart';
import '../services/dynamics/dynamics_service.dart';
import '../services/message_notification_count_service.dart';
import '../services/my_offers_service.dart';
import '../services/notifications_list_service.dart';
import '../services/profile_details_service.dart';
import '/services/app_string_service.dart';
import '/services/auth/sign_up_service.dart';
import '/services/intro_service.dart';
import 'web_socket_service.dart';

class Providers {
  static List<SingleChildWidget> providers = [
    // ── Core / Theme ──────────────────────────────────────────────────────────
    ChangeNotifierProvider(create: (context) => DynamicsService()),
    ChangeNotifierProvider(create: (context) => AppStringService()),
    ChangeNotifierProvider(create: (context) => IntroService()),

    // ── Real-time transport ───────────────────────────────────────────────────
    // Plain Provider, not ChangeNotifierProvider: the socket surfaces events
    // through a Stream rather than notifyListeners, and services listen to it.
    Provider<WebSocketService>(create: (context) => WebSocketService()),

    // ── Auth ──────────────────────────────────────────────────────────────────
    ChangeNotifierProvider(create: (context) => SignUpService()),
    ChangeNotifierProvider(create: (context) => SignInService()),
    ChangeNotifierProvider(create: (context) => OtpService()),
    ChangeNotifierProvider(create: (context) => SignOutService()),
    ChangeNotifierProvider(create: (context) => ChangePasswordService()),
    // `ResetPasswordService` was deleted. It posted
    // `{email, otp, password, confirm_password}` to `/api/auth/reset-password`,
    // whose real body is `{uid, token, new_password, confirm_password}` and
    // whose grant arrives by emailed link -- every key in that body was wrong, so
    // the call could only ever 400. `ForgotPasswordView` now drives the endpoint
    // that actually issues the grant.
    ChangeNotifierProvider(create: (context) => AccountDeleteService()),

    // ── Profile ───────────────────────────────────────────────────────────────
    ChangeNotifierProvider(create: (context) => ProfileInfoService()),
    ChangeNotifierProvider(create: (context) => ProfileEditService()),
    ChangeNotifierProvider(create: (context) => ProfileDetailsService()),
    // Payout destination. `PUT` was not implemented in the network layer at all,
    // so this route pair was previously unreachable from the app.
    ChangeNotifierProvider(create: (context) => PaymentSettingsService()),
    // FCM. Registered even when Firebase is unavailable -- the service detects
    // that itself and leaves `available` false, so the settings screen can say
    // "push is not configured" instead of the app failing to start.
    ChangeNotifierProvider(create: (context) => PushNotificationService()),
    ChangeNotifierProvider(create: (context) => NotificationPreferencesService()),

    // ── Projects / Projects ───────────────────────────────────────────────────────
    ChangeNotifierProvider(create: (context) => ProjectListService()),
    ChangeNotifierProvider(create: (context) => ProjectDetailsService()),

    // ── Contracts (Orders) ────────────────────────────────────────────────────
    ChangeNotifierProvider(create: (context) => MyContractsListService()),
    ChangeNotifierProvider(create: (context) => ContractDetailsService()),

    // ── Proposals & Offers ────────────────────────────────────────────────────
    ChangeNotifierProvider(create: (context) => MyProposalsService()),
    ChangeNotifierProvider(create: (context) => MyOffersService()),

    // ── Chat ──────────────────────────────────────────────────────────────────
    ChangeNotifierProvider(create: (context) => ChatListService()),
    ChangeNotifierProvider(create: (context) => ConversationService()),
    ChangeNotifierProvider(create: (context) => MessageNotificationCountService()),

    // ── Notifications ─────────────────────────────────────────────────────────
    ChangeNotifierProvider(create: (context) => NotificationsListService()),

    // ── Support ───────────────────────────────────────────────────────────────
    ChangeNotifierProvider(create: (context) => TicketListService()),
    ChangeNotifierProvider(create: (context) => SupportTicketCreateService()),

    // ── Filters ───────────────────────────────────────────────────────────────
    ChangeNotifierProvider(create: (context) => CategoryDropdownService()),
    ChangeNotifierProvider(create: (context) => SkillDropdownService()),

    // ── Dashboard ─────────────────────────────────────────────────────────────
    ChangeNotifierProvider(create: (context) => TodoService()),
    ChangeNotifierProvider(create: (context) => RecommendationsService()),
    ChangeNotifierProvider(create: (context) => InvitationService()),
    // Replaces the dead `SubmitProposalService`, which posted PascalCase body
    // keys (`ProjectId`, `PackageId`, ...) that the server's snake_case binder
    // silently dropped, and fetched the profile with no auth header.
    ChangeNotifierProvider(create: (context) => ProposalService()),
  ];
}
