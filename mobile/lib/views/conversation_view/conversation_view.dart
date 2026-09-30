import 'package:flutter/material.dart';
import 'package:provider/provider.dart';
import 'package:webofwork/helper/extension/context_extension.dart';
import 'package:webofwork/helper/local_keys.g.dart';
import 'package:webofwork/helper/web_socket_service.dart';
import 'package:webofwork/models/chat_list_model.dart';
import 'package:webofwork/services/conversation_service.dart';
import 'package:webofwork/services/profile_info_service.dart';
import 'package:webofwork/utils/components/custom_future_widget.dart';
import 'package:webofwork/view_models/conversation_view_model/conversation_view_model.dart';

import '../../utils/components/navigation_pop_icon.dart';
import '/views/conversation_view/components/conversations_input_box.dart';
import 'components/conversation_message_list.dart';
import 'components/conversation_skeleton.dart';

/// One chat thread.
///
/// Takes a [Chats] room straight from `ChatListService` instead of five loose
/// positional arguments. The old call passed
/// `[id, name, imageUrl, clientId, freelancerId]` and read them back with
/// `arguments[0]`..`arguments[4]`, so any caller that omitted one -- the
/// notification deep link, for instance -- threw a `RangeError`, and nothing
/// could reach `contract_id` or `contract_status` at all.
///
/// Two things are wired up here that previously never happened:
///  * `attachSocket` -- the send path needs the socket instance without a
///    `BuildContext`, and the old view never passed one, so every send fell
///    through to the REST fallback and the socket was never joined.
///  * `adoptSelfUserId` -- "is this my message?" is decided by comparing
///    `sender_id` with the signed-in user's id, which comes from
///    `ProfileInfoService`.
///
/// The scroll listener lives in `initState` and is removed in `dispose`.
/// Registering it in `build` (as this screen used to) added a new listener on
/// every rebuild, so a thread that rebuilt a dozen times had a dozen live
/// listeners all paging the same controller.
class ConversationView extends StatefulWidget {
  static const routeName = "conversation_view";
  const ConversationView({super.key});

  @override
  State<ConversationView> createState() => _ConversationViewState();
}

class _ConversationViewState extends State<ConversationView> {
  VoidCallback? _onScroll;

  @override
  void initState() {
    super.initState();
    _onScroll = () {
      if (!mounted) return;
      ConversationViewModel.instance.tryLoadingMore(context);
    };
    ConversationViewModel.instance.scrollController.addListener(_onScroll!);
  }

  @override
  void dispose() {
    if (_onScroll != null) {
      ConversationViewModel.instance.scrollController.removeListener(_onScroll!);
    }
    _onScroll = null;
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final room = context.arguments is Chats ? context.arguments as Chats : null;

    if (room == null) {
      // Reached only by a malformed deep link. Say so rather than throwing.
      return Scaffold(
        appBar: AppBar(
          leading: const NavigationPopIcon(),
          title: Text(LocalKeys.inbox),
        ),
        body: Center(child: Text(LocalKeys.noConversationsToDisplay)),
      );
    }

    final cProvider = context.read<ConversationService>();
    final selfId = context.read<ProfileInfoService>().profileInfoModel.data?.id;
    final counterpart = room.counterpartFor(selfId);
    final counterpartName = (counterpart?.name ?? '').trim();

    cProvider
      ..adoptSelfUserId(selfId)
      // Send / typing / delete go straight to the socket; the service falls back
      // to REST when the socket reports it is not connected.
      ..attachSocket(context.read<WebSocketService>());

    return Scaffold(
      backgroundColor: context.dProvider.black9,
      appBar: AppBar(
        leading: const NavigationPopIcon(),
        title: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          mainAxisSize: MainAxisSize.min,
          children: [
            Text(
              counterpartName.isEmpty ? LocalKeys.inbox : counterpartName,
              style: context.titleMedium,
            ),
            // Silent while connected; only explains a degraded state.
            Consumer<ConversationService>(
              builder: (context, cs, _) => cs.isSocketConnected
                  ? const SizedBox.shrink()
                  : Padding(
                      padding: const EdgeInsets.only(top: 2),
                      child: Text(
                        LocalKeys.reconnecting,
                        style: context.bodySmall
                            ?.copyWith(color: context.dProvider.warningColor),
                      ),
                    ),
            ),
          ],
        ),
      ),
      body: CustomFutureWidget(
        function: cProvider.shouldAutoFetch(room.id)
            ? () => cProvider.fetchConversationMessages(
                room.id.toString(),
                context: context,
              )
            : null,
        shimmer: const ConversationSkeleton(),
        child: Consumer<ConversationService>(
          builder: (context, cs, child) => Column(
            children: [
              Expanded(
                child: ConversationMessageList(
                  cs: cs,
                  selfUserId: selfId,
                  counterpartName: counterpartName,
                  counterpartImage: counterpart?.image,
                ),
              ),
              const ConversationInputBox(),
            ],
          ),
        ),
      ),
    );
  }
}
