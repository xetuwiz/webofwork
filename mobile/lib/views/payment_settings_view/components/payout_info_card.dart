import 'package:flutter/material.dart';
import 'package:webofwork/helper/extension/context_extension.dart';
import 'package:webofwork/helper/extension/int_extension.dart';
import 'package:webofwork/helper/local_keys.g.dart';
import 'package:webofwork/utils/components/app_card.dart';

/// Read-only card showing where payouts currently go.
///
/// Shown even when nothing has been set, so a freelancer who has never configured
/// an address still sees where the panel is and what it is for -- an entirely
/// empty screen reads as a broken feature.
class PayoutInfoCard extends StatelessWidget {
  final String? email;
  const PayoutInfoCard({super.key, this.email});

  @override
  Widget build(BuildContext context) {
    final hasEmail = email != null && email!.isNotEmpty;

    return AppCard(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              Icon(
                Icons.account_balance_wallet_outlined,
                size: 20,
                color: context.dProvider.primaryColor,
              ),
              10.toWidth,
              Expanded(
                child: Text(
                  LocalKeys.activePaypalEmail,
                  style: context.titleSmall?.bold6,
                ),
              ),
            ],
          ),
          10.toHeight,
          Text(
            hasEmail ? email! : LocalKeys.notSet,
            style: context.titleMedium?.copyWith(
              fontWeight: FontWeight.w600,
              // A muted colour for the empty case so "Not set" does not read as
              // a real value.
              color: hasEmail ? null : context.dProvider.black5,
            ),
          ),
          8.toHeight,
          Text(
            LocalKeys.fundsAreSentToThisEmail,
            style: context.titleSmall
                ?.copyWith(color: context.dProvider.black5),
          ),
        ],
      ),
    );
  }
}

/// Explains the escrow -> pending balance -> PayPal path.
///
/// The web client carries this copy on the same screen. Without it a freelancer
/// whose first payout has not landed has no way to tell a slow transfer from a
/// broken address.
class PayoutExplainer extends StatelessWidget {
  const PayoutExplainer({super.key});

  @override
  Widget build(BuildContext context) {
    return AppCard(
      // The brand wash marks this as an advisory rather than a stored value, so it
      // is visibly different from `PayoutInfoCard` above it.
      tinted: true,
      background: context.dProvider.primary05,
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(LocalKeys.howPayoutsWork, style: context.titleSmall?.bold6),
          6.toHeight,
          Text(
            LocalKeys.payoutExplanation,
            style: context.titleSmall?.copyWith(height: 1.5),
          ),
        ],
      ),
    );
  }
}
