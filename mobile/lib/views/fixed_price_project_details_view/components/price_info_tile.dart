import 'package:flutter/material.dart';
import 'package:webofwork/utils/components/app_card.dart';
import 'package:webofwork/helper/extension/context_extension.dart';
import 'package:webofwork/helper/extension/string_extension.dart';

class PriceInfoTile extends StatelessWidget {
  final price;
  final status;
  final color;
  const PriceInfoTile({super.key, this.price, this.status, this.color});

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.all(20),
      decoration: BoxDecoration(
        borderRadius: BorderRadius.circular(AppRadius.card),
        color: color,
      ),
      child: Row(
        mainAxisAlignment: MainAxisAlignment.spaceBetween,
        children: [
          Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text(
                "$price".cur,
                style: context.titleMedium?.copyWith().bold6,
              ),
              Text(
                "$status",
                style: context.titleSmall?.copyWith(color: context.dProvider.black5),
              )
            ],
          ),
          IconButton(
              onPressed: () {},
              icon: Icon(
                Icons.info_outline_rounded,
                color: context.dProvider.black5,
              ))
        ],
      ),
    );
  }
}
