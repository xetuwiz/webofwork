import 'package:flutter/material.dart';
import 'package:provider/provider.dart';
import 'package:webofwork/helper/extension/context_extension.dart';
import 'package:webofwork/services/support_ticket_create_service.dart';

import '../../services/ticket_list_service.dart';

/// Form state for the support-ticket create screen.
///
/// Three fields were removed because the API has no such field:
///   * `titleController` — no `title` on `CreateSupportTicketRequest`.
///   * `priority` + `department` — no such columns. `department` was the actual
///     blocker: `createTicket` returned early with "Select department" until a
///     department was chosen, and the list to choose from came from
///     `GET /api/support/departments/`, which does not exist. Ticket creation
///     was therefore impossible.
///   * `descriptionController` — renamed to `messageController` to match the
///     wire field.
class SupportTicketViewModel {
  /// Owned by the view's State, not by this singleton.
  GlobalKey<FormState>? formKey;
  ScrollController scrollController = ScrollController();
  TextEditingController subjectController = TextEditingController();
  TextEditingController messageController = TextEditingController();
  ValueNotifier<bool> loading = ValueNotifier(false);

  SupportTicketViewModel._init();
  static SupportTicketViewModel? _instance;
  static SupportTicketViewModel get instance {
    _instance ??= SupportTicketViewModel._init();
    return _instance!;
  }

  static bool get dispose {
    _instance = null;
    return true;
  }

  /// Pages in more tickets when the list hits the bottom.
  void tryToLoadMore(BuildContext context) {
    try {
      if (!scrollController.hasClients) return;
      final tlProvider = Provider.of<TicketListService>(context, listen: false);
      if (scrollController.offset >= scrollController.position.maxScrollExtent &&
          !scrollController.position.outOfRange) {
        if (tlProvider.hasMore && !tlProvider.nextPageLoading) {
          tlProvider.fetchNextPage();
        }
      }
    } catch (_) {}
  }

  void createTicket(BuildContext context) {
    if (formKey?.currentState?.validate() != true) return;
    if (loading.value) return;

    loading.value = true;

    Provider.of<SupportTicketCreateService>(context, listen: false)
        .createTicket(
      subject: subjectController.text.trim(),
      message: messageController.text.trim(),
    )
        .then((created) async {
      if (created == true && context.mounted) {
        await Provider.of<TicketListService>(context, listen: false).fetchTicketList();
        dispose;
        context.popFalse;
      }
      loading.value = false;
    });
  }
}
