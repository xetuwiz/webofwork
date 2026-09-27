import 'package:flutter/material.dart';
import 'package:provider/provider.dart';
import 'package:url_launcher/url_launcher.dart' as url_launcher;
import 'package:webofwork/helper/extension/context_extension.dart';
import 'package:webofwork/helper/extension/int_extension.dart';
import 'package:webofwork/helper/extension/string_extension.dart';
import 'package:webofwork/helper/extension/widget_extension.dart';
import 'package:webofwork/helper/local_keys.g.dart';
import 'package:webofwork/services/contract_details_service.dart';

import 'contract_details_section_card.dart';

/// The signed service agreement, when the server generated one.
///
/// `ContractResponse.legal_document_url` points at the Cloudinary-hosted PDF
/// produced at acceptance. The reference renders it as a "Service Agreement"
/// card with an explicit download link; before this card the field existed on
/// the model but nothing surfaced it, so the document was unreachable from the
/// app.
class ContractDetailsAgreement extends StatelessWidget {
  const ContractDetailsAgreement({super.key});

  @override
  Widget build(BuildContext context) {
    return Consumer<ContractDetailsService>(builder: (context, od, child) {
      final contract = od.contract;
      final url = contract?.legalDocumentUrl;
      if (contract == null || url == null || url.trim().isEmpty) {
        return const SizedBox.shrink();
      }

      return ContractDetailsSectionCard(
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(
              LocalKeys.serviceAgreement,
              style: context.titleMedium?.bold6,
            ).hp20,
            6.toHeight,
            Text(
              LocalKeys.agreementNote,
              style: context.titleSmall?.black5,
            ).hp20,
            12.toHeight,
            OutlinedButton.icon(
              onPressed: () => _open(context, url),
              icon: const Icon(Icons.download_rounded, size: 18),
              label: Text(LocalKeys.downloadAgreement),
            ).hp20,
          ],
        ),
      );
    });
  }

  Future<void> _open(BuildContext context, String url) async {
    final uri = Uri.tryParse(url);
    if (uri == null || !await url_launcher.canLaunchUrl(uri)) {
      LocalKeys.invalidUrl.showToast();
      return;
    }
    await url_launcher.launchUrl(uri,
        mode: url_launcher.LaunchMode.externalApplication);
  }
}