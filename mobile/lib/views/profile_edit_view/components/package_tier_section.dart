import 'package:flutter/material.dart';
import 'package:webofwork/utils/components/app_card.dart';
import 'package:flutter/services.dart';
import 'package:webofwork/helper/extension/context_extension.dart';
import 'package:webofwork/helper/extension/int_extension.dart';
import 'package:webofwork/helper/local_keys.g.dart';
import 'package:webofwork/utils/components/field_with_label.dart';

/// One service package ("starter" / "pro"): price, delivery time and what the
/// package includes.
///
/// A tier is only sent to the API once it has a price -- the server creates the
/// row on demand and never overwrites a price with zero, so an all-blank card
/// would otherwise leave a phantom package behind.
class PackageTierSection extends StatelessWidget {
  final String title;
  final String subtitle;
  final TextEditingController priceController;
  final TextEditingController daysController;
  final TextEditingController descriptionController;
  final String? Function(String?)? priceValidator;
  final String? Function(String?)? daysValidator;

  const PackageTierSection({
    super.key,
    required this.title,
    required this.subtitle,
    required this.priceController,
    required this.daysController,
    required this.descriptionController,
    this.priceValidator,
    this.daysValidator,
  });

  @override
  Widget build(BuildContext context) {
    return Container(
      width: double.infinity,
      padding: const EdgeInsets.all(16),
      decoration: BoxDecoration(
        borderRadius: BorderRadius.circular(AppRadius.card),
        border: Border.all(color: context.dProvider.black8),
        color: context.dProvider.primary05,
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              Text(
                title,
                style: context.titleMedium?.bold6,
              ),
              8.toWidth,
              Text(
                '(${LocalKeys.optional})',
                style: context.titleSmall?.copyWith(color: context.dProvider.black5),
              ),
            ],
          ),
          4.toHeight,
          Text(
            subtitle,
            style: context.titleSmall?.copyWith(color: context.dProvider.black5),
          ),
          16.toHeight,
          Row(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Expanded(
                child: FieldWithLabel(
                  label: LocalKeys.price,
                  hintText: '0',
                  controller: priceController,
                  keyboardType: const TextInputType.numberWithOptions(
                    decimal: true,
                  ),
                  validator: priceValidator,
                ),
              ),
              12.toWidth,
              Expanded(
                child: FieldWithLabel(
                  label: LocalKeys.deliveryDays,
                  hintText: '7',
                  controller: daysController,
                  keyboardType: TextInputType.number,
                  inputFormatters: [FilteringTextInputFormatter.digitsOnly],
                  validator: daysValidator,
                ),
              ),
            ],
          ),
          FieldWithLabel(
            label: LocalKeys.description,
            hintText: LocalKeys.enterPackageDescription,
            controller: descriptionController,
            keyboardType: TextInputType.multiline,
            minLines: 3,
            maxLines: 5,
          ),
        ],
      ),
    );
  }
}
