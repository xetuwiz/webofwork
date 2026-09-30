import 'package:flutter/material.dart';
import 'package:provider/provider.dart';
import 'package:webofwork/helper/extension/context_extension.dart';
import 'package:webofwork/helper/extension/int_extension.dart';
import 'package:webofwork/services/conversation_service.dart';
import 'package:webofwork/view_models/conversation_view_model/conversation_view_model.dart';

import '/helper/local_keys.g.dart';
import 'conversations_buttons.dart';

/// Composer.
///
/// `SafeArea(top: false)` sits **inside** the decorated container: the app is
/// edge-to-edge, so without it the send row and the system navigation bar
/// overlap.
class ConversationInputBox extends StatelessWidget {
  const ConversationInputBox({super.key});

  @override
  Widget build(BuildContext context) {
    final cm = ConversationViewModel.instance;
    return Container(
      decoration: BoxDecoration(
        color: context.dProvider.whiteColor,
        border: Border(top: BorderSide(color: context.dProvider.black7)),
      ),
      child: SafeArea(
        top: false,
        child: Padding(
          padding: const EdgeInsets.all(20),
          child: Column(
            children: [
              // Fires `{"type":"typing","is_typing":true}` on the room socket while
              // there is text, and `false` the moment the field is cleared.
              TypingSignal(controller: cm.messageController),
              TextFormField(
                controller: cm.messageController,
                textInputAction: TextInputAction.newline,
                keyboardType: TextInputType.multiline,
                minLines: 1,
                maxLines: 6,
                decoration: InputDecoration(
                  hintText: '${LocalKeys.message}...',
                ),
              ),
              16.toHeight,
              const ConversationButtons(),
            ],
          ),
        ),
      ),
    );
  }
}

/// Broadcasts the composer's emptiness to the room's typing channel.
///
/// There is no server-side presence store: `setPeerTyping` de-dupes against the
/// last value *it* sent, so this only puts a frame on the wire when the state
/// actually changes.
class TypingSignal extends StatelessWidget {
  final TextEditingController controller;
  const TypingSignal({super.key, required this.controller});

  @override
  Widget build(BuildContext context) {
    return ValueListenableBuilder<TextEditingValue>(
      valueListenable: controller,
      builder: (context, value, _) {
        context.read<ConversationService>().setPeerTyping(
              value.text.trim().isNotEmpty,
            );
        return const SizedBox.shrink();
      },
    );
  }
}
