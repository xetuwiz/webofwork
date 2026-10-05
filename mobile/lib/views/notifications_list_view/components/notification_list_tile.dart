import 'package:flutter/material.dart';
import 'package:provider/provider.dart';
import 'package:webofwork/helper/extension/context_extension.dart';
import 'package:webofwork/helper/extension/datetime_extension.dart';
import 'package:webofwork/helper/extension/int_extension.dart';
import 'package:webofwork/helper/extension/string_extension.dart';
import 'package:webofwork/helper/svg_assets.dart';
import 'package:webofwork/models/notification_list_model.dart';
import 'package:webofwork/services/notifications_list_service.dart';

import '../../chat_list_view/chat_list_view.dart';
import '../../invitations_list_view/invitations_list_view.dart';
import '../../my_contract_details_view/my_contract_details_view.dart';
import '../../my_proposals_view/my_proposals_view.dart';
import 'package:webofwork/helper/local_keys.g.dart';

/// One row in the notifications list.
///
/// Takes the parsed [AppNotification] rather than five loose fields, because the
/// tap target is derived from `related_entity_type` + `related_entity_id` -- the
/// two fields the previous model did not parse at all, which is why tapping a
/// notification did nothing.
class NotificationListTile extends StatelessWidget {
  final AppNotification notification;

  const NotificationListTile({super.key, required this.notification});

  /// Values `PublishAsync` is actually called with across the backend
  /// (NotificationService callers): "Contract", "Proposal", "Invitation",
  /// "Dispute", "Review", "Payment", "ChatRoom". `RelatedEntityType` is a
  /// `string?`, so the global snake_case enum converter does not apply to the
  /// value -- it arrives verbatim and PascalCase.
  String? get _relatedType => notification.relatedEntityType?.trim().toLowerCase();

  bool get _hasTargetId => notification.relatedEntityId != null;

  void _onTap(BuildContext context) {
    // Marking is the first thing a tap does (whether or not it navigates), so
    // a user who glances at a notification and taps it no longer leaves the
    // whole list unread. State flips once `PATCH /api/notifications/{id}/read`
    // answers.
    final service = context.read<NotificationsListService>();
    service.markRead(context, notification);

    if (!_hasTargetId) return;
    final id = notification.relatedEntityId;

    switch (_relatedType) {
      case 'contract':
        // `MyContractDetailsView` reads the id from
        // `ModalRoute.of(context)!.settings.arguments`, so pass it as a route
        // argument. The old code pushed the screen with no argument and instead
        // stashed the id in a global via `setOrderId`, which
        // `MyContractDetailsView` never reads -- so the contract screen opened with
        // a null id.
        context.toNamed(MyContractDetailsView.routeName, arguments: id);
        break;
      case 'proposal':
        context.toNamed(MyProposalsView.routeName);
        break;
      case 'invitation':
        context.toNamed(InvitationsListView.routeName);
        break;
      case 'chatroom':
        // The id is a room id, but ConversationView resolves its room through
        // the provider rather than a route argument, so land on the list.
        context.toNamed(ChatListView.routeName);
        break;
      // 'dispute' carries a dispute id, not a contract id, so there is no
      // screen to open. 'review' and 'payment' have no dedicated screen.
      default:
        break;
    }
  }

  @override
  Widget build(BuildContext context) {
    final isRead = notification.isRead;
    final tappable = _hasTargetId &&
        _relatedType != null &&
        const {
          'contract',
          'proposal',
          'invitation',
          'chatroom',
        }.contains(_relatedType);

    return InkWell(
      onTap: tappable ? () => _onTap(context) : null,
      child: Container(
        padding: const EdgeInsets.all(20),
        color: context.dProvider.whiteColor,
        child: Row(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            // Unread rows are the ones that need attention, so the brand tint and
            // the full-strength message belong to them. This had it backwards: a
            // *read* notification was rendered in brand blue with a bright
            // timestamp, so the list read as "everything I have already handled is
            // the important bit".
            CircleAvatar(
              radius: 20,
              backgroundColor:
                  isRead ? context.dProvider.black9 : context.dProvider.primary05,
              child: SvgAssets.notification.toSVGSized(
                20,
                color: isRead ? context.dProvider.black5 : context.dProvider.primaryColor,
              ),
            ),
            const SizedBox(width: 16),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(
                    notification.message,
                    maxLines: 3,
                    // Unread is emphasised, read recedes. Muted rather than a
                    // reduced opacity so the text stays above the contrast floor.
                    style: context.titleMedium?.bold6.copyWith(
                      color: isRead ? context.dProvider.black5 : null,
                    ),
                  ),
                  8.toHeight,
                  Text(
                    (notification.createdAt ?? DateTime.now()).friendlyDateTime,
                    style: context.titleSmall?.copyWith(
                      color: isRead
                          ? context.dProvider.black5
                          : context.dProvider.primaryColor,
                    ),
                  ),
                ],
              ),
            ),
            // An explicit mark-read on rows that cannot navigate anywhere
            // ('dispute', 'review', 'payment' have no screen). Navigable rows
            // are marked by tapping them, so they omit the button.
            if (!isRead && !tappable)
              IconButton(
                tooltip: LocalKeys.markAsRead,
                visualDensity: VisualDensity.compact,
                icon: Icon(
                  Icons.check_circle_outline,
                  size: 20,
                  color: context.dProvider.primaryColor,
                ),
                onPressed: () =>
                    context.read<NotificationsListService>().markRead(context, notification),
              ),
          ],
        ),
      ),
    );
  }
}
