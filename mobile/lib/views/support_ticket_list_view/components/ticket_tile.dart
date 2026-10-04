import 'package:flutter/material.dart';
import 'package:webofwork/utils/components/app_card.dart';
import 'package:provider/provider.dart';
import 'package:webofwork/helper/extension/context_extension.dart';
import 'package:webofwork/helper/extension/datetime_extension.dart';
import 'package:webofwork/helper/extension/int_extension.dart';
import 'package:webofwork/helper/extension/string_extension.dart';
import 'package:webofwork/models/ticket_list_model.dart';
import 'package:webofwork/utils/components/alerts.dart';

import '../../../helper/local_keys.g.dart';
import '../../../helper/svg_assets.dart';
import '../../../services/ticket_list_service.dart';

/// One support ticket row.
///
/// Built from `SupportTicketResponse`:
/// `{id, subject, message, status, admin_reply, created_at, updated_at, …}`.
///
/// Three things changed here:
///   * Tapping the row used to push `TicketChatView` and simultaneously fire
///     `TicketChatService.fetchSingleTickets`, which hit a per-ticket messages
///     route that does not exist. The support model is "one question, at most
///     one admin reply" -- there is no thread -- so the reply is shown inline
///     instead and tapping just expands the row.
///   * The `priority` chip is gone. `SupportTicket` has no priority column; the
///     colour was chosen from a `low/medium/high/urgent` map that never had an
///     entry to match, so every ticket fell back to the first colour.
///   * The trailing button had **no** `onTap`, so it looked actionable and did
///     nothing. It is now a real Cancel, gated on `status == 'open'` to match
///     the API's "Only open support tickets can be cancelled."
class TicketTile extends StatelessWidget {
  final Ticket ticket;
  const TicketTile({super.key, required this.ticket});

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.all(20),
      decoration: BoxDecoration(
        borderRadius: BorderRadius.circular(AppRadius.card),
        color: context.dProvider.whiteColor,
        border: Border.all(color: context.dProvider.black8),
      ),
      child: Theme(
        data: Theme.of(context).copyWith(dividerColor: Colors.transparent),
        child: ExpansionTile(
          tilePadding: EdgeInsets.zero,
          childrenPadding: const EdgeInsets.only(top: 8),
          expandedCrossAxisAlignment: CrossAxisAlignment.start,
          shape: const Border(),
          collapsedShape: const Border(),
          title: Row(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text(
                '#${ticket.id}',
                style: context.titleSmall
                    ?.copyWith(color: context.dProvider.primaryColor)
                    .bold6,
              ),
              10.toWidth,
              Expanded(
                child: Text(
                  ticket.subject,
                  maxLines: 2,
                  overflow: TextOverflow.ellipsis,
                  style: context.titleMedium,
                ),
              ),
            ],
          ),
          subtitle: Padding(
            padding: const EdgeInsets.only(top: 8),
            child: Row(
              children: [
                _StatusPill(status: ticket.status),
                10.toWidth,
                Expanded(
                  child: Text(
                    _formatDate(ticket.createdAt),
                    style: context.titleSmall?.copyWith(color: context.dProvider.black5),
                  ),
                ),
                if (ticket.isOpen) _CancelButton(ticketId: ticket.id),
              ],
            ),
          ),
          children: [
            Divider(color: context.dProvider.black8, thickness: 2, height: 20),
            Text(ticket.message, style: context.titleSmall),
            if (ticket.hasReply) ...[
              16.toHeight,
              Container(
                width: double.infinity,
                padding: const EdgeInsets.all(14),
                decoration: BoxDecoration(
                  borderRadius: BorderRadius.circular(AppRadius.card),
                  color: context.dProvider.primary05,
                ),
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(
                      LocalKeys.replyFromSupport,
                      style: context.titleSmall?.bold6,
                    ),
                    8.toHeight,
                    Text(
                      ticket.adminReply!,
                      style:
                          context.titleSmall?.copyWith(color: context.dProvider.black7),
                    ),
                  ],
                ),
              ),
            ],
          ],
        ),
      ),
    );
  }

  static String _formatDate(DateTime? d) {
    if (d == null) return '';
    return d.shortDate;
  }
}

class _StatusPill extends StatelessWidget {
  final String status;
  const _StatusPill({required this.status});

  @override
  Widget build(BuildContext context) {
    final isOpen = status == 'open';
    final isCancelled = status == 'cancelled';
    final color = isOpen
        ? context.dProvider.greenColor
        : (isCancelled ? context.dProvider.black5 : context.dProvider.primaryColor);
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 4),
      decoration: BoxDecoration(
        borderRadius: BorderRadius.circular(AppRadius.card),
        color: color,
      ),
      child: Text(
        status.capitalize,
        style: context.titleSmall
            ?.copyWith(color: context.dProvider.whiteColor, fontSize: 13)
            .bold6,
      ),
    );
  }
}

class _CancelButton extends StatelessWidget {
  final int ticketId;
  const _CancelButton({required this.ticketId});

  @override
  Widget build(BuildContext context) {
    return InkWell(
      onTap: () {
        Alerts().confirmationAlert(
          context: context,
          title: LocalKeys.areYouSure,
          description: LocalKeys.cancelTicketConfirm,
          buttonText: LocalKeys.confirm,
          buttonColor: context.dProvider.warningColor,
          onConfirm: () async {
            await Provider.of<TicketListService>(context, listen: false)
                .cancelTicket(ticketId);
          },
        );
      },
      borderRadius: BorderRadius.circular(10),
      child: Container(
        padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 6),
        decoration: BoxDecoration(
          borderRadius: BorderRadius.circular(10),
          color: context.dProvider.primaryColor,
        ),
        child: SvgAssets.invisible.toSVGSized(16, color: context.dProvider.whiteColor),
      ),
    );
  }
}
