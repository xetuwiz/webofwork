import 'package:flutter/material.dart';
import 'package:webofwork/helper/extension/context_extension.dart';
import 'package:webofwork/helper/extension/int_extension.dart';

class MyContractId extends StatelessWidget {
  final id;
  const MyContractId({
    super.key,
    this.id,
  });

  @override
  Widget build(BuildContext context) {
    return Wrap(
      crossAxisAlignment: WrapCrossAlignment.center,
      children: [
        Text(
          id.toString(),
          style:
              context.titleSmall?.bold6.copyWith(color: context.dProvider.primaryColor),
        ),
        6.toWidth,
      ],
    );
  }
}
