import 'package:flutter/material.dart';
import 'package:provider/provider.dart';
import 'package:webofwork/helper/extension/context_extension.dart';
import 'package:webofwork/helper/extension/int_extension.dart';
import 'package:webofwork/helper/extension/datetime_extension.dart';
import 'package:webofwork/helper/extension/widget_extension.dart';
import 'package:webofwork/helper/local_keys.g.dart';
import 'package:webofwork/services/contract_details_service.dart';

import 'contract_details_section_card.dart';

/// The project brief plus the requirements and skills the contract was created
/// from.
///
/// `ContractResponse` carries `project_description`, `requirements`,
/// `expected_deliverables` and `skills` (aliased `required_technologies`). The
/// expected-deliverables block is shown alongside the submitted work so the two
/// can be compared, and the skills are listed because the brief usually ends
/// "must know X, Y, Z" and a freelancer checking scope wants that in one place.
class ContractDetailsDescription extends StatelessWidget {
  const ContractDetailsDescription({super.key});

  @override
  Widget build(BuildContext context) {
    return Consumer<ContractDetailsService>(builder: (context, od, child) {
      final contract = od.contract;
      if (contract == null) return const SizedBox.shrink();

      return ContractDetailsSectionCard(
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(LocalKeys.orderDescription, style: context.titleMedium?.bold6).hp20,
            8.toHeight,
            Text(
              contract.projectDescription.trim().isEmpty
                  ? LocalKeys.noDescriptionAvailable
                  : contract.projectDescription,
              style: context.titleSmall?.black5,
            ).hp20,
            if (contract.requirements != null &&
                contract.requirements!.trim().isNotEmpty) ...[
              Divider(
                color: context.dProvider.black9,
                thickness: 1,
                height: 32,
              ),
              Text(
                LocalKeys.projectRequirements,
                style: context.titleSmall?.bold6,
              ).hp20,
              6.toHeight,
              Text(contract.requirements!, style: context.titleSmall?.black5).hp20,
            ],
            if (contract.skills.isNotEmpty) ...[
              Divider(
                color: context.dProvider.black9,
                thickness: 1,
                height: 32,
              ),
              Text(LocalKeys.requiredSkills, style: context.titleSmall?.bold6).hp20,
              8.toHeight,
              Wrap(
                spacing: 8,
                runSpacing: 8,
                children: contract.skills
                    .map(
                      (s) => Container(
                        padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 4),
                        decoration: BoxDecoration(
                          color: context.dProvider.black9,
                          borderRadius: BorderRadius.circular(20),
                        ),
                        child: Text(s, style: context.titleSmall?.black5),
                      ),
                    )
                    .toList(),
              ).hp20,
            ],
            if (contract.clientHasSigned || contract.freelancerHasSigned) ...[
              Divider(
                color: context.dProvider.black9,
                thickness: 1,
                height: 32,
              ),
              Text(LocalKeys.signatures, style: context.titleSmall?.bold6).hp20,
              8.toHeight,
              _SignatureRow(
                label: LocalKeys.clientSignature,
                signed: contract.clientHasSigned,
                at: contract.clientSignedAt,
                awaiting: LocalKeys.awaitingClientSignature,
              ).hp20,
              6.toHeight,
              _SignatureRow(
                label: LocalKeys.freelancerSignature,
                signed: contract.freelancerHasSigned,
                at: contract.freelancerSignedAt,
                awaiting: LocalKeys.awaitingYourSignature,
              ).hp20,
            ],
          ],
        ),
      );
    });
  }
}

class _SignatureRow extends StatelessWidget {
  const _SignatureRow({
    required this.label,
    required this.signed,
    required this.awaiting,
    this.at,
  });

  final String label;
  final bool signed;
  final DateTime? at;

  /// Shown in place of the date while unsigned, e.g. "Awaiting your signature"
  /// for the freelancer row.
  final String awaiting;

  @override
  Widget build(BuildContext context) {
    final color = signed ? context.dProvider.greenColor : context.dProvider.black5;
    return Row(
      children: [
        Icon(
          signed ? Icons.check_circle_rounded : Icons.pending_outlined,
          size: 18,
          color: color,
        ),
        8.toWidth,
        Expanded(
          child: Text(
            signed
                ? '$label${at == null ? '' : ' • ${at!.shortDate}'}'
                : '$label • $awaiting',
            style: context.titleSmall?.copyWith(color: color),
          ),
        ),
      ],
    );
  }
}
