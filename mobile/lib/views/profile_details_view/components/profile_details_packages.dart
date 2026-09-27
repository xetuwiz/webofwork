import 'package:flutter/material.dart';
import 'package:webofwork/helper/extension/context_extension.dart';
import 'package:webofwork/helper/extension/int_extension.dart';
import 'package:webofwork/helper/extension/widget_extension.dart';
import 'package:webofwork/helper/local_keys.g.dart';
import 'package:webofwork/models/profile_details_model.dart';
import 'package:webofwork/services/profile_details_service.dart';

/// Renders the two package tiers from
/// `GET /api/profile/freelancer/profile` -> `packages: { starter, pro }`.
///
/// Nothing read this before, so the tiers a freelancer had configured were
/// invisible everywhere in the app even though the API returns them.
class ProfileDetailsPackages extends StatelessWidget {
  final ProfileDetailsService pd;
  const ProfileDetailsPackages({super.key, required this.pd});

  @override
  Widget build(BuildContext context) {
    final profile = pd.profileDetails;
    final tiers = <MapEntry<String, Package?>>[
      MapEntry(LocalKeys.starter, profile.starter),
      MapEntry(LocalKeys.pro, profile.pro),
    ].where((e) => e.value != null).toList();

    if (tiers.isEmpty) return const SizedBox();

    return Container(
      margin: const EdgeInsets.only(top: 20),
      padding: const EdgeInsets.symmetric(vertical: 20),
      decoration: BoxDecoration(
        borderRadius: BorderRadius.circular(8),
        color: context.dProvider.whiteColor,
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(LocalKeys.packages, style: context.titleMedium?.bold6).hp20,
          Divider(
            color: context.dProvider.black8,
            thickness: 2,
            height: 36,
          ),
          ...tiers.map((e) => _Tier(
                name: e.key,
                package: e.value!,
              )),
        ],
      ),
    );
  }
}

class _Tier extends StatelessWidget {
  final String name;
  final Package package;
  const _Tier({required this.name, required this.package});

  @override
  Widget build(BuildContext context) {
    return Container(
      margin: const EdgeInsets.symmetric(horizontal: 20, vertical: 8),
      padding: const EdgeInsets.all(16),
      decoration: BoxDecoration(
        borderRadius: BorderRadius.circular(8),
        border: Border.all(color: context.dProvider.black8),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            mainAxisAlignment: MainAxisAlignment.spaceBetween,
            children: [
              Text(name, style: context.titleMedium?.bold6),
              Text(
                package.price.toStringAsFixed(2),
                // `bold6` returns a non-null `TextStyle`, so the old second `?.`
                // could never short-circuit -- it was dead, and dropping it is
                // what makes this compile.
                style: context.titleMedium?.bold6
                    .copyWith(color: context.dProvider.primaryColor),
              ),
            ],
          ),
          if (package.deliveryDays > 0) ...[
            6.toHeight,
            Text(
              '${package.deliveryDays} ${LocalKeys.days}',
              style: context.titleSmall?.copyWith(color: context.dProvider.black5),
            ),
          ],
          if (package.description.isNotEmpty) ...[
            10.toHeight,
            ...package.description.map(
              (line) => Padding(
                padding: const EdgeInsets.only(bottom: 4),
                child: Row(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text('• ',
                        style: context.titleSmall
                            ?.copyWith(color: context.dProvider.black5)),
                    Expanded(
                      child: Text(
                        line,
                        style:
                            context.titleSmall?.copyWith(color: context.dProvider.black5),
                      ),
                    ),
                  ],
                ),
              ),
            ),
          ],
        ],
      ),
    );
  }
}
