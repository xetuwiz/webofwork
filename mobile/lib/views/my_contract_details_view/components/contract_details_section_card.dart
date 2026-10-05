import 'package:flutter/material.dart';
import 'package:webofwork/utils/components/app_card.dart';
import 'package:webofwork/helper/extension/context_extension.dart';

/// The white rounded panel that each contract-details section sits in.
///
/// Extracted because the details screen is now four sibling cards (summary,
/// description, deliverables, revisions) rather than one, and repeating the same
/// `Container` + `BoxDecoration` per section is how the four drifted apart in
/// border radius and border colour in the first place.
class ContractDetailsSectionCard extends StatelessWidget {
  const ContractDetailsSectionCard({super.key, required this.child});

  final Widget child;

  @override
  Widget build(BuildContext context) {
    return Container(
      width: double.infinity,
      padding: const EdgeInsets.symmetric(vertical: 20),
      decoration: BoxDecoration(
        borderRadius: BorderRadius.circular(AppRadius.card),
        color: context.dProvider.whiteColor,
        border: Border.all(color: context.dProvider.black9),
      ),
      child: child,
    );
  }
}
