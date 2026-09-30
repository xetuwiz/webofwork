import 'package:flutter/material.dart';
import 'package:provider/provider.dart';
import 'package:webofwork/helper/extension/context_extension.dart';
import 'package:webofwork/helper/local_keys.g.dart';
import 'package:webofwork/services/chat_list_service.dart';
import 'package:webofwork/services/profile_info_service.dart';
import 'package:webofwork/view_models/chat_list_view_model/chat_list_view_model.dart';

import '../../utils/components/custom_future_widget.dart';
import '../../utils/components/custom_refresh_indicator.dart';
import '../../utils/components/empty_widget.dart';
import 'components/chat_tile.dart';
import '/views/chat_list_view/components/chat_tiles_shimmer.dart';
import 'package:webofwork/utils/components/navigation_pop_icon.dart';

/// `GET /api/chat/rooms` -> `{ data: [ ChatRoomResponse ] }`.
///
/// Rewritten against the real payload. The previous body read
/// `chatListModel.chatList?.chats` (the template's key was `chat_list`, not
/// `data`), `activeUsers`, `activityCheck[clientId]` and
/// `"${profileImagePath}/${client.image}"` -- none of which exist, so every
/// room came out blank-named with no preview and no badge.
///
/// The list is **not** paged: the endpoint has no `next` link, so the infinite
/// scroll and its `nextPage` footer are gone. The scroll listener moves to
/// `initState` for the same reason it did in the conversation screen: adding it
/// in `build` leaked a listener per rebuild.
class ChatListView extends StatefulWidget {
  static const routeName = 'chat_list_view';
  const ChatListView({super.key});

  @override
  State<ChatListView> createState() => _ChatListViewState();
}

class _ChatListViewState extends State<ChatListView> {
  VoidCallback? _onScroll;

  @override
  void initState() {
    super.initState();
    _onScroll = () {
      if (!mounted) return;
      // Refresh on overscroll so a new message that arrived while the user was
      // elsewhere shows up without a manual pull.
      final clv = ChatListViewModel.instance;
      if (clv.scrollController.hasClients && clv.scrollController.offset <= 0) {
        context.read<ChatListService>().fetchChatList();
      }
    };
    ChatListViewModel.instance.scrollController.addListener(_onScroll!);
  }

  @override
  void dispose() {
    if (_onScroll != null) {
      ChatListViewModel.instance.scrollController.removeListener(_onScroll!);
    }
    _onScroll = null;
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final clProvider = context.read<ChatListService>();
    final selfId = context.read<ProfileInfoService>().profileInfoModel.data?.id;
    final clv = ChatListViewModel.instance;

    return Scaffold(
      backgroundColor: context.dProvider.whiteColor,
      appBar: AppBar(
        // Reached from the dashboard message button and from a notification tile,
        // so it is a pushed route, not a tab root.
        leading: const NavigationPopIcon(),
        title: Text(LocalKeys.inbox),
      ),
      body: CustomRefreshIndicator(
        onRefresh: () async {
          await clProvider.fetchChatList();
        },
        child: CustomFutureWidget(
          function: clProvider.shouldAutoFetch ? clProvider.fetchChatList : null,
          shimmer: const ChatTileShimmer(),
          child: Consumer<ChatListService>(
            builder: (context, cl, child) {
              final rooms = cl.rooms;
              if (rooms.isEmpty) {
                return ListView(
                  // Keeps pull-to-refresh working on the empty state.
                  physics: const AlwaysScrollableScrollPhysics(),
                  children: [
                    SizedBox(height: context.height * 0.2),
                    EmptyWidget(title: LocalKeys.noConversationsToDisplay),
                  ],
                );
              }
              return Scrollbar(
                controller: clv.scrollController,
                child: Container(
                  color: context.dProvider.whiteColor,
                  padding: const EdgeInsets.all(20),
                  child: ListView.separated(
                    padding: EdgeInsets.zero,
                    physics: const AlwaysScrollableScrollPhysics(),
                    controller: clv.scrollController,
                    itemCount: rooms.length,
                    separatorBuilder: (context, index) => Divider(
                      color: context.dProvider.black8,
                      thickness: 1,
                    ),
                    itemBuilder: (context, index) =>
                        ChatTile(room: rooms[index], selfUserId: selfId),
                  ),
                ),
              );
            },
          ),
        ),
      ),
    );
  }
}
