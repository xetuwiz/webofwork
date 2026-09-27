import 'dart:async';

import 'package:flutter/material.dart';
import 'package:provider/provider.dart';
import 'package:webofwork/helper/extension/context_extension.dart';
import 'package:webofwork/helper/extension/datetime_extension.dart';
import 'package:webofwork/helper/extension/int_extension.dart';
import 'package:webofwork/helper/extension/widget_extension.dart';
import 'package:webofwork/helper/local_keys.g.dart';
import 'package:webofwork/models/contract_model.dart';
import 'package:webofwork/services/contract_details_service.dart';

import 'contract_details_section_card.dart';

/// The production timeline: when work started, when it hard-stops, and the
/// escrow state, mirroring the "Production Timeline" card on the reference
/// contract detail.
///
/// The clock starts at the freelancer's signature (falling back to creation)
/// and runs for `delivery_days` working days. A completed contract pins the
/// bar at 100% and the counter at zero, exactly like the reference
/// (`completed -> timeLeft 0/0/100`).
class ContractDetailsTimeline extends StatefulWidget {
  const ContractDetailsTimeline({super.key});

  @override
  State<ContractDetailsTimeline> createState() => _ContractDetailsTimelineState();
}

class _ContractDetailsTimelineState extends State<ContractDetailsTimeline> {
  Timer? _ticker;

  @override
  void initState() {
    super.initState();
    // Refresh the countdown so it does not freeze for the screen's lifetime.
    _ticker = Timer.periodic(const Duration(seconds: 30), (_) {
      if (mounted) setState(() {});
    });
  }

  @override
  void dispose() {
    _ticker?.cancel();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return Consumer<ContractDetailsService>(builder: (context, od, child) {
      final contract = od.contract;
      if (contract == null) return const SizedBox.shrink();

      final signedAt = contract.freelancerSignedAt ?? contract.createdAt;
      final completed = contract.status == Contract.statusCompleted;
      final disputed = contract.status == Contract.statusDisputed;

      final _TimeLeft left;
      if (signedAt == null || contract.deliveryDays <= 0 || completed) {
        left = _TimeLeft(days: 0, hours: 0, percentage: completed ? 100 : 0);
      } else {
        // ISO strings parse as UTC instants; subtracting them from a local
        // `DateTime.now()` is still an absolute difference.
        final total = Duration(days: contract.deliveryDays);
        final maturity = signedAt.add(total);
        final remaining = maturity.difference(DateTime.now());
        final elapsed = total - remaining;
        final percentage = ((elapsed.inMilliseconds / total.inMilliseconds) * 100)
            .clamp(0.0, 100.0)
            .toDouble();
        left = _TimeLeft(
          days: remaining.isNegative ? 0 : remaining.inDays,
          hours: remaining.isNegative
              ? 0
              : remaining.inHours.remainder(24),
          percentage: percentage,
        );
      }

      return ContractDetailsSectionCard(
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(
              LocalKeys.productionTimeline,
              style: context.titleMedium?.bold6,
            ).hp20,
            12.toHeight,
            Row(
              children: [
                Text(
                  LocalKeys.untilMaturity,
                  style: context.titleSmall?.black5,
                ),
                const Spacer(),
                if (disputed)
                  Text(
                    LocalKeys.clockPaused,
                    style:
                        context.titleSmall?.copyWith(color: Colors.amber.shade800),
                  )
                else
                  Text(
                    "${left.days}d : ${left.hours}h",
                    style: context.titleSmall?.bold6,
                  ),
              ],
            ).hp20,
            8.toHeight,
            ClipRRect(
              borderRadius: BorderRadius.circular(4),
              child: LinearProgressIndicator(
                value: left.percentage / 100,
                minHeight: 8,
                backgroundColor: const Color(0xFFF1F5F9),
                valueColor: AlwaysStoppedAnimation(
                  completed
                      ? const Color(0xFF22C55E)
                      : disputed
                          ? Colors.amber.shade400
                          : const Color(0xFF2563EB),
                ),
              ),
            ).hp20,
            12.toHeight,
            Text(
              LocalKeys.fundsInEscrow,
              style: context.titleSmall
                  ?.copyWith(color: const Color(0xFF1D4ED8)),
            ).hp20,
            12.toHeight,
            Divider(color: context.dProvider.black9, height: 24),
            _summaryRow(
              context,
              label: LocalKeys.commencement,
              value: signedAt?.shortDate ?? '-',
            ),
            _summaryRow(
              context,
              label: LocalKeys.hardDeadline,
              value: signedAt == null || contract.deliveryDays <= 0
                  ? '-'
                  : signedAt
                      .add(Duration(days: contract.deliveryDays))
                      .toLocal()
                      .shortDate,
            ),
            _summaryRow(
              context,
              label: LocalKeys.deliveryDays,
              value: '${contract.deliveryDays} ${LocalKeys.workingDays}',
            ),
            _summaryRow(
              context,
              label: LocalKeys.escrowStatus,
              value: contract.isFunded
                  ? LocalKeys.funded
                  : LocalKeys.notFunded,
            ),
            if (contract.payoutEligibleAfter != null)
              _summaryRow(
                context,
                label: LocalKeys.payoutEligibleAfter,
                value: contract.payoutEligibleAfter!.toLocal().shortDate,
              ),
          ],
        ),
      );
    });
  }

  Widget _summaryRow(BuildContext context, {required String label, required String value}) {
    return Padding(
      padding: const EdgeInsets.symmetric(vertical: 4),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Expanded(
            child: Text(
              label,
              style: context.titleSmall?.black5,
            ),
          ),
          Flexible(
            child: Text(
              value,
              textAlign: TextAlign.end,
              style: context.titleSmall?.bold6,
            ),
          ),
        ],
      ).hp20,
    );
  }
}

class _TimeLeft {
  const _TimeLeft({
    required this.days,
    required this.hours,
    required this.percentage,
  });

  final int days;
  final int hours;
  final double percentage;
}