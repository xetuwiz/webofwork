import 'package:flutter/material.dart';
import 'package:provider/provider.dart';
import 'package:webofwork/helper/extension/context_extension.dart';
import 'package:webofwork/helper/extension/int_extension.dart';
import 'package:webofwork/helper/local_keys.g.dart';
import 'package:webofwork/models/conversation_model.dart';
import 'package:webofwork/services/conversation_service.dart';
import 'package:webofwork/utils/components/alerts.dart';
import 'package:webofwork/utils/components/custom_preloader.dart';
import 'package:webofwork/utils/components/empty_widget.dart';
import 'package:webofwork/view_models/conversation_view_model/conversation_view_model.dart';

import 'chat_bubble.dart';

/// The message thread for one room.
///
/// Three things were wrong here and are now fixed:
///
///  * `cs.nextPage` did not exist (the endpoint is cursor-paged), so the
///    "load older" spinner and `itemCount` were compile errors.
///  * `senderFromWeb: datum.fromUser.toString() == 1.toString()` compared a
///    field the API does not send. Left-alignment is now decided by comparing
///    `sender_id` with the signed-in user's id, which is what `sender_id`
///    actually carries.
///  * `debugPrint((piProvider.profileInfoModel.data?.id).toString())` printed
///    the user id on **every row of every message** -- a flood of log output
///    for no reason.
///
/// The list is `reverse: true` with oldest-first data, so index 0 renders at the
/// bottom; a "load older" footer therefore belongs at the *last* index.
class ConversationMessageList extends StatelessWidget {
  final ConversationService cs;
  final String counterpartName;
  final String? counterpartImage;
  final String? selfUserId;

  const ConversationMessageList({
    super.key,
    required this.cs,
    required this.counterpartName,
    required this.selfUserId,
    this.counterpartImage,
  });

  @override
  Widget build(BuildContext context) {
    final messages = cs.conversationModel.allMessage?.data ?? const <Datum>[];
    if (messages.isEmpty && !cs.nextPageLoading) {
      return EmptyWidget(title: LocalKeys.noMessageFound);
    }

    final showFooter = cs.hasMore && !cs.nexLoadingFailed;
    final itemCount = messages.length + (showFooter ? 1 : 0);

    return Stack(
      children: [
        ListView.separated(
          controller: ConversationViewModel.instance.scrollController,
          padding: const EdgeInsets.symmetric(vertical: 12),
          reverse: true,
          itemCount: itemCount,
          separatorBuilder: (context, index) => 4.toHeight,
          itemBuilder: (context, index) {
            if (showFooter && index == itemCount - 1) {
              return const Padding(
                padding: EdgeInsets.all(16),
                child: Center(child: CustomPreloader(width: 40)),
              );
            }
            // `reverse: true` means the visually-first (oldest) message is the
            // last list index.
            final datum = messages[messages.length - 1 - index];
            final isMine = selfUserId != null &&
                datum.senderId != null &&
                datum.senderId == selfUserId;
            return ChatBubble(
              key: ValueKey(datum.id ?? index),
              datum: datum,
              senderFromWeb: !isMine,
              counterpartName: counterpartName,
              counterpartImage: counterpartImage,
              onLongPress: isMine && datum.id != null
                  ? () => _confirmDelete(context, datum.id!)
                  : null,
            );
          },
        ),
        if (cs.isPeerTyping)
          Positioned(
            left: 20,
            bottom: 8,
            child: _TypingBubble(name: counterpartName),
          ),
      ],
    );
  }

  void _confirmDelete(BuildContext context, int messageId) {
    final cs = Provider.of<ConversationService>(context, listen: false);
    Alerts().confirmationAlert(
      context: context,
      title: LocalKeys.deleteMessage,
      description: LocalKeys.deleteMessageConfirm,
      buttonText: LocalKeys.delete,
      buttonColor: Theme.of(context).colorScheme.error,
      onConfirm: () async {
        // The socket is the fast path; REST is the fallback when it is down.
        if (!cs.deleteMessage(messageId)) {
          await cs.deleteMessageOverRest(messageId);
        }
      },
    );
  }
}

/// "… is typing".
///
/// The `setPeerTyping` flag is broadcast to the peer over the socket; there is
/// no server-side presence store, so the indicator only appears for traffic that
/// actually arrives on the room socket.
class _TypingBubble extends StatelessWidget {
  final String name;
  const _TypingBubble({required this.name});

  @override
  Widget build(BuildContext context) {
    final label = '${name.trim().split(' ').first} ${LocalKeys.isTyping}';
    return IgnorePointer(
      child: Container(
        padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 8),
        decoration: BoxDecoration(
          color: context.dProvider.whiteColor,
          borderRadius: BorderRadius.circular(14),
        ),
        child: Row(
          mainAxisSize: MainAxisSize.min,
          children: [
            SizedBox(
              width: 34,
              child: Text(
                label,
                maxLines: 1,
                overflow: TextOverflow.ellipsis,
                style: context.titleSmall?.copyWith(color: context.dProvider.black6),
              ),
            ),
            8.toWidth,
            Text(
              '•••',
              style: context.titleMedium?.copyWith(color: context.dProvider.primaryColor),
            ),
          ],
        ),
      ),
    );
  }
}
