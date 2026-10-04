import 'package:flutter/material.dart';
import 'package:provider/provider.dart';
import 'package:webofwork/helper/extension/int_extension.dart';
import 'package:webofwork/helper/local_keys.g.dart';
import 'package:webofwork/services/notifications_list_service.dart';
import 'package:webofwork/utils/components/custom_future_widget.dart';
import 'package:webofwork/utils/components/custom_refresh_indicator.dart';
import 'package:webofwork/utils/components/empty_widget.dart';
import 'package:webofwork/view_models/notifications_list_view_model/notifications_list_view_model.dart';
import 'package:webofwork/views/notifications_list_view/components/notification_list_skeleton.dart';
import 'package:webofwork/views/notifications_list_view/components/notification_list_tile.dart';
import '/helper/extension/context_extension.dart';
import 'package:webofwork/utils/components/navigation_pop_icon.dart';

class NotificationListView extends StatefulWidget {
  static const routeName = 'notification_list_view';
  const NotificationListView({super.key});

  @override
  State<NotificationListView> createState() => _NotificationListViewState();
}

class _NotificationListViewState extends State<NotificationListView> {
  final _scrollController = NotificationsListViewModel.instance.scrollController;

  @override
  void initState() {
    super.initState();
    // Was registered inside build(), which added a new listener on every
    // rebuild and never removed the old ones.
    _scrollController.addListener(_onScroll);
  }

  @override
  void dispose() {
    _scrollController.removeListener(_onScroll);
    super.dispose();
  }

  void _onScroll() {
    if (!mounted) return;
    NotificationsListViewModel.instance.tryToLoadMore(context);
  }

  @override
  Widget build(BuildContext context) {
    final nlProvider = Provider.of<NotificationsListService>(context, listen: false);

    return Scaffold(
      backgroundColor: context.dProvider.whiteColor,
      appBar: AppBar(
        // Reached from the push-notification banner and from a tray tap.
        leading: const NavigationPopIcon(),
        title: Text(LocalKeys.notifications),
        actions: [
          // Manual "mark all read". The list used to mark everything read
          // automatically on first load, which emptied the badge before the
          // user saw a single row; the user now chooses.
          Consumer<NotificationsListService>(
            builder: (context, nl, child) {
              final unread = nl.notificationsList.unreadCount;
              if (unread <= 0) return const SizedBox.shrink();
              return TextButton(
                onPressed: () => nl.markAllRead(context),
                child: Text(LocalKeys.markAllRead),
              );
            },
          ),
        ],
      ),
      body: Column(
        children: [
          4.toHeight,
          // Deliberately no ProfileInfoService gate here. The list does not
          // read anything from the profile, and gating on
          // `profileInfoModel.data == null` meant a failed profile fetch pinned
          // this screen to a permanent AccountSkeleton.
          Expanded(
            child: CustomRefreshIndicator(
              onRefresh: () => nlProvider.fetchNotificationsList(context),
              child: CustomFutureWidget(
                function: nlProvider.shouldAutoFetch
                    ? () => nlProvider.fetchNotificationsList(context)
                    : null,
                shimmer: const NotificationListSkeleton(),
                child: Consumer<NotificationsListService>(
                  builder: (context, nl, child) {
                    final items = nl.notificationsList.notifications ?? [];
                    if (items.isEmpty) {
                      return EmptyWidget(title: LocalKeys.noNotificationYet);
                    }
                    return Scrollbar(
                      controller: _scrollController,
                      child: ListView.separated(
                        controller: _scrollController,
                        physics: const AlwaysScrollableScrollPhysics(),
                        padding: const EdgeInsets.symmetric(vertical: 20),
                        itemCount: items.length,
                        separatorBuilder: (context, index) => 4.toHeight,
                        itemBuilder: (context, index) =>
                            NotificationListTile(notification: items[index]),
                      ),
                    );
                  },
                ),
              ),
            ),
          ),
        ],
      ),
    );
  }
}
