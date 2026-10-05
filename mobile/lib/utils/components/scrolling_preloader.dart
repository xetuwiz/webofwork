import 'package:flutter/material.dart';
import 'package:webofwork/helper/extension/context_extension.dart';
import 'package:webofwork/helper/local_keys.g.dart';

class ScrollPreloader extends StatelessWidget {
  final bool whiteColor;
  final double? width;
  final bool loading;
  final String? text;
  final IconData? iconData;
  const ScrollPreloader({
    super.key,
    this.whiteColor = false,
    this.width,
    required this.loading,
    this.text,
    this.iconData,
  });

  @override
  Widget build(BuildContext context) {
    switch (loading) {
      case false:
        return Column(
          children: [
            Icon(
              iconData ?? Icons.arrow_circle_up_rounded,
              color: context.dProvider.black6,
            ),
            Text(
              text ?? LocalKeys.pullUp,
              style: context.titleSmall?.copyWith(
                color: context.dProvider.black6,
              ),
            ),
          ],
        );
      default:
        return Container(
            width: width,
            height: whiteColor ? null : 36,
            alignment: Alignment.center,
            child: CircularProgressIndicator(
              color: whiteColor ? Colors.white : Theme.of(context).primaryColor,
              strokeWidth: 2,
            ));
    }
  }
}
