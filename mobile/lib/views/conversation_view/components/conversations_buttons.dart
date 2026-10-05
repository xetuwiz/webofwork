import 'package:flutter/material.dart';
import 'package:webofwork/helper/extension/context_extension.dart';
import 'package:webofwork/helper/extension/int_extension.dart';
import 'package:webofwork/helper/extension/string_extension.dart';
import 'package:webofwork/utils/components/custom_preloader.dart';
import 'package:webofwork/view_models/conversation_view_model/conversation_view_model.dart';

import '../../../helper/local_keys.g.dart';
import '../../../helper/svg_assets.dart';

/// Send row.
///
/// The attachment button was removed: the chat API has no attachment field, so
/// the picker could only ever produce a file that was then thrown away. The
/// remaining button sends text; the composer above it drives the typing
/// indicator over the room socket (`{"type":"typing"}`).
class ConversationButtons extends StatelessWidget {
  const ConversationButtons({super.key});

  @override
  Widget build(BuildContext context) {
    final cm = ConversationViewModel.instance;
    return Row(
      children: [
        Expanded(
          flex: 16,
          child: ValueListenableBuilder(
            valueListenable: cm.loading,
            builder: (context, loading, child) => ElevatedButton(
              // Disabled while a send is in flight so a double-tap cannot post
              // the same text twice.
              onPressed: loading ? null : () => cm.trySendingMessage(context),
              child: loading
                  ? const Center(child: CustomPreloader(width: 80, whiteColor: true))
                  : Row(
                      mainAxisAlignment: MainAxisAlignment.center,
                      children: [
                        SvgAssets.send
                            .toSVGSized(18, color: context.dProvider.whiteColor),
                        6.toWidth,
                        Flexible(
                          child: Text(
                            LocalKeys.sendMessage,
                            maxLines: 1,
                            overflow: TextOverflow.ellipsis,
                          ),
                        ),
                      ],
                    ),
            ),
          ),
        ),
      ],
    );
  }
}
