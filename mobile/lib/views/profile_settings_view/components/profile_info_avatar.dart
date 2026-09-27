import 'package:flutter/material.dart';
import 'package:provider/provider.dart';
import 'package:webofwork/services/profile_info_service.dart';
import '/views/chat_list_view/components/chat_tile_avatar.dart';

/// The signed-in user's avatar.
///
/// There is no image-picker path here any more. The backend has no
/// profile-photo upload endpoint, and `UpdateFreelancerProfileRequest` has no
/// `profile_photo` field, so a picked image could never be stored -- the old
/// "Change Photo" button opened the gallery and then POSTed the file to a route
/// that only accepts GET. Showing the current photo (or initials) is honest;
/// offering an upload that silently discards the result is not.
class ProfileInfoAvatar extends StatelessWidget {
  const ProfileInfoAvatar({super.key});

  @override
  Widget build(BuildContext context) {
    return Consumer<ProfileInfoService>(
      builder: (context, piProvider, child) {
        return SizedBox(
          width: 100,
          height: 100,
          child: Center(
            child: ChatTileAvatar(
              name: piProvider.profileInfoModel.data?.firstName ?? "FE",
              imageUrl: piProvider.profileInfoModel.data?.image,
              size: 90.0,
            ),
          ),
        );
      },
    );
  }
}
