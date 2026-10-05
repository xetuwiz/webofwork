import 'package:flutter/material.dart';
import 'package:provider/provider.dart';
import 'package:webofwork/helper/extension/context_extension.dart';
import 'package:webofwork/helper/extension/string_extension.dart';
import 'package:webofwork/helper/local_keys.g.dart';
import 'package:webofwork/services/conversation_service.dart';

/// Form state for the conversation screen.
///
/// The attachment picker was removed: `ChatMessageResponse` has no attachment
/// field and `MessageRequest` binds only `Content`, so a picked file could
/// never be transmitted. `trySendingMessage` had a `File?` parameter that was
/// accepted and then dropped, and `attachFile` also had an inverted null check
/// (`if (file == null && file!.files.isNotEmpty)`) that would have thrown on
/// cancel.
///
/// `initPusher` was an empty stub left over from the deleted Pusher client.
class ConversationViewModel {
  TextEditingController messageController = TextEditingController();
  ValueNotifier<bool> loading = ValueNotifier(false);

  ScrollController scrollController = ScrollController();

  ConversationViewModel._init();
  static ConversationViewModel? _instance;
  static ConversationViewModel get instance {
    _instance ??= ConversationViewModel._init();
    return _instance!;
  }

  static bool get dispose {
    _instance = null;
    return true;
  }

  void trySendingMessage(BuildContext context) async {
    context.unFocus;
    if (messageController.text.trim().isEmpty) {
      LocalKeys.pleaseWriteAFirstMessage.showToast();
      return;
    }
    if (loading.value) return;
    loading.value = true;

    final cProvider = Provider.of<ConversationService>(context, listen: false);
    final sent = await cProvider.trySendingMessage(messageController.text);
    if (sent) messageController.clear();
    loading.value = false;
  }

  /// Pages in older messages when the list is scrolled to the top.
  ///
  /// Read `hasMore` off the service: the previous `nextPage` getter no longer
  /// exists because this endpoint is cursor-paged (`before_message_id`), not
  /// link-paged, so there is no URL to follow.
  void tryLoadingMore(BuildContext context) {
    try {
      if (!scrollController.hasClients) return;
      final cs = Provider.of<ConversationService>(context, listen: false);
      if (scrollController.offset <= 0 &&
          !scrollController.position.outOfRange &&
          cs.hasMore &&
          !cs.nextPageLoading) {
        cs.fetchNextPage();
      }
    } catch (_) {}
  }
}
