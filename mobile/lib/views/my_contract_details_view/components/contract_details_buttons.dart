import 'package:flutter/material.dart';
import 'package:provider/provider.dart';
import 'package:webofwork/helper/extension/context_extension.dart';
import 'package:webofwork/helper/extension/int_extension.dart';
import 'package:webofwork/helper/local_keys.g.dart';
import 'package:webofwork/models/contract_model.dart';
import 'package:webofwork/services/chat_list_service.dart';
import 'package:webofwork/services/contract_details_service.dart';
import 'package:webofwork/utils/components/custom_button.dart';
import 'package:webofwork/views/conversation_view/conversation_view.dart';

/// The actions available on a contract.
///
/// Which buttons appear is derived from the real state machine
/// (`ContractStateMachine`) rather than from integer status codes:
///
/// ```text
/// offered   -> active | cancelled
/// active    -> submitted | cancelled | disputed
/// submitted -> completed | active | disputed
/// disputed  -> completed | refunded
/// completed / cancelled / refunded -> (terminal)
/// ```
///
/// Both `Offered -> Cancelled` and `Active -> Cancelled` are reachable, so the
/// freelancer can decline an offer and cancel a running contract with the same
/// action. `PATCH /api/contracts/{id}/status` is the route; it used to be
/// `[Authorize(Policy = "RequireClient")]`, so a freelancer got 403 on both.
///
/// The chat button was previously **unreachable**: it posted
/// `{contractId: …}` (camelCase) and then pushed `ConversationView` with five
/// positional arguments, which the rebuilt `ConversationView` -- now taking one
/// `Chats` object through `context.arguments` -- can no longer read.
class ContractDetailsButtons extends StatelessWidget {
  const ContractDetailsButtons({super.key, required this.contract});

  final Contract contract;

  /// True while the contract is `offered` and the freelancer has not signed.
  bool get _needsSignature => contract.needsMySignature;

  /// The state machine permits cancelling from `offered` or `active` only.
  bool get _canCancel =>
      contract.status == Contract.statusOffered ||
      contract.status == Contract.statusActive;

  /// Chat is only possible in the statuses `ChatService.ChatAllowedStatuses`
  /// permits -- `active`, `submitted`, `disputed` (the server 403s any other
  /// room-open, including on an unsigned `offered` contract). The old check
  /// `!contract.isClosed` also allowed `offered`, so the button was shown but
  /// every tap died on the server.
  bool get _canChat =>
      contract.status == Contract.statusActive ||
      contract.status == Contract.statusSubmitted ||
      contract.status == Contract.statusDisputed;

  @override
  Widget build(BuildContext context) {
    if (!_needsSignature && !_canCancel && !_canChat) {
      return const SizedBox.shrink();
    }

    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        if (_needsSignature) ...[
          SizedBox(
            width: double.infinity,
            child: ElevatedButton(
              onPressed: () => _accept(context),
              style: ElevatedButton.styleFrom(
                backgroundColor: context.dProvider.primaryColor,
                padding: const EdgeInsets.symmetric(vertical: 14),
              ),
              child: Text(LocalKeys.acceptContract),
            ),
          ),
          8.toHeight,
        ],
        if (_canCancel)
          SizedBox(
            width: double.infinity,
            child: CustomButton(
              onPressed: () => _cancel(context),
              btText:
                  _needsSignature ? LocalKeys.declineContract : LocalKeys.cancelContract,
              isLoading: false,
              backgroundColor: context.dProvider.warningColor,
            ),
          ),
        if (_canChat) ...[
          8.toHeight,
          SizedBox(
            width: double.infinity,
            child: ElevatedButton(
              onPressed: () => _openChat(context),
              style: ElevatedButton.styleFrom(
                backgroundColor: context.dProvider.primaryColor,
                padding: const EdgeInsets.symmetric(vertical: 14),
              ),
              child: Text(LocalKeys.chatWithClient),
            ),
          ),
        ],
      ],
    );
  }

  Future<void> _accept(BuildContext context) async {
    final messenger = ScaffoldMessenger.of(context);
    final service = context.read<ContractDetailsService>();
    final ok = await service.tryAcceptOrder(orderId: contract.id);
    if (!context.mounted) return;
    if (!ok) {
      // The failure reason was already toasted by the network layer.
      return;
    }
    messenger.showSnackBar(
      SnackBar(content: Text(LocalKeys.contractAccepted)),
    );
  }

  Future<void> _cancel(BuildContext context) async {
    final confirmed = await showDialog<bool>(
      context: context,
      builder: (context) => AlertDialog(
        title: Text(LocalKeys.cancelContract),
        content: Text(LocalKeys.cancelContractConfirm),
        actions: [
          TextButton(
            onPressed: () => context.popFalse,
            child: Text(LocalKeys.no),
          ),
          TextButton(
            onPressed: () => context.popTrue,
            child: Text(LocalKeys.yes),
          ),
        ],
      ),
    );
    if (confirmed != true || !context.mounted) return;

    final service = context.read<ContractDetailsService>();
    await service.tryUpdateStatus(
      orderId: contract.id,
      status: Contract.statusCancelled,
    );
  }

  Future<void> _openChat(BuildContext context) async {
    final chat = context.read<ChatListService>();
    final navigator = Navigator.of(context);

    final room = await chat.getOrCreateRoomForContract(contract.id);
    if (!context.mounted) return;
    if (room == null) {
      // `postApi` already surfaced the server's message.
      return;
    }
    navigator.pushNamed(ConversationView.routeName, arguments: room);
  }
}
