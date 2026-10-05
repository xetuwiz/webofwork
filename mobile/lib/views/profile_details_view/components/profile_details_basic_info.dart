import 'package:flutter/material.dart';
import 'package:provider/provider.dart';
import 'package:webofwork/helper/extension/context_extension.dart';
import 'package:webofwork/helper/extension/int_extension.dart';
import 'package:webofwork/services/profile_details_service.dart';

import '../../../helper/local_keys.g.dart';
import 'profile_name_infos.dart';

/// Identity block: name/avatar, contact details, experience level, member
/// since, and the bio.
///
/// Everything here is read-only. The "Available for work" switch that used to
/// sit in this card called `ProfileDetailsService.tryChangingStatus()`, which
/// POSTed to a GET-only route and always 405'd -- a toggle that could not
/// change anything. Availability is edited from the Profile Edit screen
/// (`PATCH /api/profile/freelancer/me`) and displayed in the header pill.
class ProfileDetailsBasicInfo extends StatelessWidget {
  const ProfileDetailsBasicInfo({super.key});

  @override
  Widget build(BuildContext context) {
    return Consumer<ProfileDetailsService>(builder: (context, pd, child) {
      final profile = pd.profileDetails;
      return Container(
        padding: const EdgeInsets.all(20),
        decoration: BoxDecoration(
          borderRadius: BorderRadius.circular(8),
          color: context.dProvider.whiteColor,
        ),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            const ProfileNameInfos(),
            if ((profile.phone ?? '').isNotEmpty) ...[
              Divider(
                color: context.dProvider.black8,
                thickness: 2,
                height: 36,
              ),
              Row(
                children: [
                  Icon(Icons.phone_rounded, size: 18, color: context.dProvider.black5),
                  12.toWidth,
                  Expanded(
                    child: Text(
                      profile.phone!,
                      style:
                          context.titleSmall?.copyWith(color: context.dProvider.black5),
                    ),
                  ),
                  if (profile.isPhoneVerified)
                    Icon(Icons.verified_rounded,
                        size: 18, color: context.dProvider.greenColor),
                ],
              ),
            ],
            if ((profile.experienceLevel ?? '').isNotEmpty) ...[
              Divider(
                color: context.dProvider.black8,
                thickness: 2,
                height: 36,
              ),
              _Row(
                label: LocalKeys.experienceLevel,
                value: profile.experienceLevel!,
              ),
            ],
            if (profile.createdAt != null) ...[
              Divider(
                color: context.dProvider.black8,
                thickness: 2,
                height: 36,
              ),
              _Row(
                label: LocalKeys.memberSince,
                value: _year(profile.createdAt!),
              ),
            ],
            if ((profile.bio ?? '').isNotEmpty) ...[
              Divider(
                color: context.dProvider.black8,
                thickness: 2,
                height: 36,
              ),
              Text(LocalKeys.aboutMe, style: context.titleMedium?.bold6),
              8.toHeight,
              Text(
                profile.bio!,
                style: context.titleSmall?.copyWith(color: context.dProvider.black5),
              ),
            ],
          ],
        ),
      );
    });
  }

  static String _year(DateTime d) => '${d.year}';
}

class _Row extends StatelessWidget {
  final String label;
  final String value;
  const _Row({required this.label, required this.value});

  @override
  Widget build(BuildContext context) {
    return Row(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Expanded(
          flex: 4,
          child: Text(
            label,
            style: context.titleSmall?.copyWith(color: context.dProvider.black5),
          ),
        ),
        Expanded(
          flex: 6,
          child: Text(
            value,
            textAlign: TextAlign.end,
            style: context.titleSmall?.bold6,
          ),
        ),
      ],
    );
  }
}
