import 'package:flutter/material.dart';

class CustomPreloader extends StatelessWidget {
  final bool whiteColor;
  final double? width;
  const CustomPreloader({
    super.key,
    this.whiteColor = false,
    this.width,
  });

  @override
  Widget build(BuildContext context) {
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
