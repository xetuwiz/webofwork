import 'package:flutter/material.dart';
import 'package:provider/provider.dart';
import 'package:webofwork/helper/extension/context_extension.dart';
import 'package:webofwork/helper/extension/int_extension.dart';
import 'package:webofwork/services/profile_details_service.dart';
import 'package:webofwork/views/chat_list_view/components/chat_tile_avatar.dart';

/// Avatar, name, tagline and rating, read from the flat
/// `GET /api/profile/freelancer/profile` payload.
///
/// The previous version built the image URL as
/// `"${profileImagePath}/${user?.image}"`, but this payload has no nested
/// `user` object and `profile_photo` is already a resolvable URL/path
/// (see `resolveMediaUrl`), so the avatar was always broken and the name blank.
class ProfileNameInfos extends StatelessWidget {
  const ProfileNameInfos({super.key});

  @override
  Widget build(BuildContext context) {
    return Consumer<ProfileDetailsService>(builder: (context, pd, child) {
      final profile = pd.profileDetails;
      final name = profile.fullName ?? "";
      return Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          ChatTileAvatar(
            imageUrl: profile.profilePhoto ?? "",
            name: name,
            size: 60.0,
          ),
          const SizedBox(width: 16),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                if ((profile.availability ?? '').isNotEmpty) ...[
                  _Pill(
                    color: context.dProvider.greenColor,
                    label: profile.availability!,
                  ),
                  8.toHeight,
                ],
                Text(
                  name,
                  style: context.titleMedium?.bold6,
                ),
                if ((profile.tagline ?? '').isNotEmpty) ...[
                  4.toHeight,
                  Text(
                    profile.tagline!,
                    style: context.titleSmall?.copyWith(color: context.dProvider.black5),
                  ),
                ],
                8.toHeight,
                Container(
                  padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 2),
                  decoration: BoxDecoration(
                    borderRadius: BorderRadius.circular(20),
                    color: context.dProvider.yellowColor.withOpacity(0.10),
                  ),
                  child: FittedBox(
                    child: Row(
                      children: [
                        Icon(
                          Icons.star_rounded,
                          color: context.dProvider.yellowColor,
                          size: 20,
                        ),
                        4.toWidth,
                        Text(
                          "${profile.averageRating.toStringAsFixed(1)} (${profile.reviewCount})",
                          style: context.titleSmall
                              ?.copyWith(color: context.dProvider.yellowColor)
                              .bold6,
                        ),
                      ],
                    ),
                  ),
                ),
              ],
            ),
          ),
        ],
      );
    });
  }
}

class _Pill extends StatelessWidget {
  final Color color;
  final String label;
  const _Pill({required this.color, required this.label});

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 6),
      decoration: BoxDecoration(
        borderRadius: BorderRadius.circular(20),
        border: Border.all(color: color),
      ),
      child: FittedBox(
        child: Row(
          children: [
            CircleAvatar(radius: 4, backgroundColor: color),
            4.toWidth,
            Text(
              label,
              style: context.titleSmall?.copyWith(color: color).bold6,
            ),
          ],
        ),
      ),
    );
  }
}
