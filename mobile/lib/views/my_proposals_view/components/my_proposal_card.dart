import 'package:flutter/material.dart';
import 'package:webofwork/utils/components/app_card.dart';
import 'package:webofwork/helper/extension/context_extension.dart';
import 'package:webofwork/models/my_proposals_model.dart';

import 'my_proposal_card_infos.dart';

/// One row in the My Proposals list.
///
/// Takes a parsed [MyProposal] rather than the eleven loosely-typed template
/// parameters it used to take (`isSeen`, `isInterviewed`, `isShortListed`,
/// `attachment`, .), because none of those exist on `ProposalResponse` -- they
/// were template columns. Every one of them was read as `null` and guarded with
/// `isSeen is bool` / `== true`, so the chips never appeared and the card showed
/// an id, a price, a delivery string and nothing else.
class MyProposalCard extends StatelessWidget {
  const MyProposalCard({super.key, required this.proposal});

  final MyProposal proposal;

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
      child: MyProposalCardInfos(proposal: proposal),
    );
  }
}
