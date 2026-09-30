import 'package:flutter/material.dart';
import 'package:webofwork/helper/extension/context_extension.dart';
import 'package:webofwork/helper/extension/datetime_extension.dart';
import 'package:webofwork/helper/extension/int_extension.dart';
import 'package:webofwork/helper/local_keys.g.dart';
import 'package:webofwork/models/conversation_model.dart';
import 'package:webofwork/views/chat_list_view/components/chat_tile_avatar.dart';

/// One message row.
///
/// A WebOfWork chat message is **text and nothing else**. `ChatMessageResponse`
/// is `{id, room_id, sender_id, sender_role, sender_name, text, is_read,
/// is_deleted, created_at}` -- there is no `message` sub-object, no `file`, no
/// `project` card and no offer card. The previous bubble read
/// `datum.message?.message`, `datum.message?.project?.interviewMessage` and
/// `datum.file`, and rendered `ProjectBubble` / `AttachmentBubble` /
/// `OfferBubble`; on every real payload [text] was empty, the `if` guard around
/// the `Row` was false, and the whole `Column` rendered as nothing. So a thread
/// that had loaded fine showed no messages at all. Those three bubble components
/// and the upload path are gone with it.
///
/// Deleted messages are tombstoned server-side (`is_deleted`, `text` nulled), so
/// they render as "This message was deleted" rather than an empty bubble.
///
/// `senderFromWeb` is derived from `senderId == selfUserId` at the call site
/// instead of the old `"1"` / `"2"` role codes the API never returns.
class ChatBubble extends StatelessWidget {
  final Datum datum;

  /// True when the message came from the other participant.
  final bool senderFromWeb;
  final String counterpartName;
  final String? counterpartImage;

  /// Fires when a message is long-pressed; the view opens the
  /// delete-my-own-message sheet.
  final VoidCallback? onLongPress;

  const ChatBubble({
    super.key,
    required this.datum,
    required this.senderFromWeb,
    required this.counterpartName,
    this.counterpartImage,
    this.onLongPress,
  });

  @override
  Widget build(BuildContext context) {
    final text = (datum.text ?? '').trim();
    final isDeleted = datum.isDeleted;
    // A row with neither text nor a tombstone is a malformed frame; rendering an
    // empty bubble would just add vertical whitespace.
    if (!isDeleted && text.isEmpty) return const SizedBox.shrink();

    return Padding(
      padding: const EdgeInsets.symmetric(vertical: 4),
      child: Row(
        mainAxisAlignment:
            senderFromWeb ? MainAxisAlignment.start : MainAxisAlignment.end,
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          if (senderFromWeb) ...[
            ChatTileAvatar(
              name: counterpartName,
              size: 32.0,
              imageUrl: counterpartImage,
            ),
            12.toWidth,
          ],
          Flexible(
            child: Column(
              crossAxisAlignment:
                  senderFromWeb ? CrossAxisAlignment.start : CrossAxisAlignment.end,
              children: [
                GestureDetector(
                  onLongPress: onLongPress,
                  child: Container(
                    padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 12),
                    constraints: BoxConstraints(maxWidth: context.width * 0.75),
                    decoration: BoxDecoration(
                      color: isDeleted
                          ? context.dProvider.black9
                          : (senderFromWeb
                              ? context.dProvider.whiteColor
                              : context.dProvider.primaryColor),
                      borderRadius: BorderRadius.only(
                        bottomLeft: const Radius.circular(14),
                        bottomRight: const Radius.circular(14),
                        topRight: senderFromWeb
                            ? const Radius.circular(14)
                            : const Radius.circular(2),
                        topLeft: senderFromWeb
                            ? const Radius.circular(2)
                            : const Radius.circular(14),
                      ),
                    ),
                    child: Text(
                      isDeleted ? LocalKeys.messageDeleted : text,
                      textAlign: senderFromWeb ? null : TextAlign.end,
                      style: context.titleMedium?.copyWith(
                        fontSize: 14,
                        color: isDeleted
                            ? context.dProvider.black5
                            : (senderFromWeb ? null : context.dProvider.whiteColor),
                      ),
                    ),
                  ),
                ),
                4.toHeight,
                Row(
                  mainAxisSize: MainAxisSize.min,
                  children: [
                    Text(
                      (datum.createdAt ?? DateTime.now()).friendlyDateTime,
                      style: context.bodySmall?.copyWith(color: context.dProvider.black5),
                    ),
                    // Read receipts only make sense on your own messages.
                    if (!senderFromWeb && datum.isRead) ...[
                      6.toWidth,
                      Icon(Icons.done_all,
                          size: 14, color: context.dProvider.primaryColor),
                    ],
                  ],
                ),
              ],
            ),
          ),
        ],
      ),
    );
  }
}
