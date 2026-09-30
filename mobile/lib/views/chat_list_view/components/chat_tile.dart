import 'package:flutter/material.dart';
import 'package:webofwork/utils/components/app_card.dart';
import 'package:webofwork/helper/extension/context_extension.dart';
import 'package:webofwork/helper/extension/int_extension.dart';
import 'package:webofwork/helper/local_keys.g.dart';
import 'package:webofwork/models/chat_list_model.dart';
import 'package:webofwork/view_models/conversation_view_model/conversation_view_model.dart';

import '../../../utils/components/empty_spacer_helper.dart';
import '/helper/extension/datetime_extension.dart';
import '/views/conversation_view/conversation_view.dart';
import 'chat_tile_avatar.dart';

/// One row in the inbox.
///
/// Built from [Chats] (`ChatRoomResponse`) rather than nine loose parameters.
/// The previous tile was handed `clientId`, `freelancerId`, `unRead`,
/// `activityString` and a `profileImagePath`-prefixed URL assembled by the list
/// view -- fields that exist in no DTO. `ChatParticipantDto` has no photo field
/// at all, so the avatar has always been initials-on-a-colour; the colour is
/// still derived from the room id so a given room always looks the same.
///
/// The green presence dot was driven by `chatListModel.activeUsers`, a field
/// this API never returns, so it was permanently grey. There is no presence
/// endpoint, so it is gone rather than left permanently false.
class ChatTile extends StatelessWidget {
  final Chats room;
  final String? selfUserId;
  final bool isTyping;

  const ChatTile({
    super.key,
    required this.room,
    this.selfUserId,
    this.isTyping = false,
  });

  @override
  Widget build(BuildContext context) {
    final counterpart = room.counterpartFor(selfUserId);
    final name = (counterpart?.name ?? '').trim();
    final unread = room.unreadCount;
    final stamp = room.lastMessageAt ?? room.createdAt;

    return InkWell(
      onTap: () {
        context.toNamed(
          ConversationView.routeName,
          arguments: room,
          then: () {
            // Clear the composer and the global "current room" marker so the
            // next thread does not inherit them.
            ConversationViewModel.instance.messageController.clear();
          },
        );
      },
      child: Container(
        decoration: BoxDecoration(
          borderRadius: BorderRadius.circular(AppRadius.card),
          color: unread > 0 ? context.dProvider.primary05 : null,
        ),
        padding: const EdgeInsets.all(20),
        child: Row(
          children: [
            ChatTileAvatar(
              name: name,
              imageUrl: counterpart?.image,
              color: context.dProvider.chatAvatarBGColors[
                  (room.id ?? 0) % context.dProvider.chatAvatarBGColors.length],
            ),
            EmptySpaceHelper.emptyWidth(12),
            Expanded(
              child: Column(
                mainAxisAlignment: MainAxisAlignment.start,
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Row(
                    children: [
                      Expanded(
                        child: Text(
                          name.isEmpty ? LocalKeys.inbox : name,
                          maxLines: 1,
                          overflow: TextOverflow.ellipsis,
                          style: context.titleMedium?.copyWith(
                            fontWeight: unread > 0 ? FontWeight.w700 : FontWeight.w600,
                          ),
                        ),
                      ),
                      8.toWidth,
                      if (stamp != null)
                        Text(
                          stamp.friendlyDateTime,
                          style: context.titleSmall?.copyWith(
                            color: unread > 0
                                ? context.dProvider.primaryColor
                                : context.dProvider.black5,
                          ),
                        ),
                    ],
                  ),
                  6.toHeight,
                  Row(
                    children: [
                      Expanded(
                        child: Text(
                          isTyping
                              ? LocalKeys.typing
                              : (room.lastMessagePreview ?? LocalKeys.noMessageFound),
                          maxLines: 1,
                          overflow: TextOverflow.ellipsis,
                          style: isTyping
                              ? context.titleSmall?.copyWith(
                                  color: context.dProvider.primaryColor,
                                  fontStyle: FontStyle.italic,
                                )
                              : context.titleSmall?.copyWith(
                                  color: context.dProvider.black5,
                                ),
                        ),
                      ),
                      if (unread > 0) ...[
                        8.toWidth,
                        Container(
                          padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 2),
                          decoration: BoxDecoration(
                            color: context.dProvider.primaryColor,
                            borderRadius: BorderRadius.circular(12),
                          ),
                          child: Text(
                            unread > 99 ? '99+' : '$unread',
                            style: context.titleSmall?.copyWith(
                              color: context.dProvider.whiteColor,
                              fontSize: 12,
                            ),
                          ),
                        ),
                      ],
                    ],
                  ),
                ],
              ),
            ),
          ],
        ),
      ),
    );
  }
}
