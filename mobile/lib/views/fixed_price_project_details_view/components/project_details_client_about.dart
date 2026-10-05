import 'package:flutter/material.dart';
import 'package:provider/provider.dart';
import 'package:webofwork/helper/extension/context_extension.dart';
import 'package:webofwork/helper/extension/int_extension.dart';
import 'package:webofwork/helper/extension/widget_extension.dart';
import 'package:webofwork/helper/local_keys.g.dart';
import 'package:webofwork/services/project_details_service.dart';
import 'package:webofwork/views/chat_list_view/components/chat_tile_avatar.dart';

import '../../../helper/image_assets.dart';

/// "About the client" on the job-details screen.
///
/// Built from the `client_info` sub-object of `GET /api/projects/{id}/details`:
/// `full_name`, `profile_photo`, `company_name`, `average_rating`,
/// `review_count`, `total_jobs_posted`.
///
/// Two rows are deliberately **gone** rather than faked:
///
///  * **Member since** — the template read `user.createdAt`, a key this payload
///    does not have, and fell back to `DateTime.now()`. Every project therefore
///    claimed the client joined today. There is no join date on the freelancer
///    project-detail payload, so there is nothing honest to show.
///  * **Hire rate** — the template read `hiring_rate`, also absent, and rendered
///    the literal string `"null%"`.
class ProjectDetailsClientAbout extends StatelessWidget {
  const ProjectDetailsClientAbout({super.key});

  @override
  Widget build(BuildContext context) {
    return Consumer<ProjectDetailsService>(builder: (context, jd, child) {
      final client = jd.projectDetailsModel.projectDetails?.client;
      if (client == null) return const SizedBox.shrink();

      return Container(
        padding: const EdgeInsets.symmetric(vertical: 20),
        decoration: BoxDecoration(
          borderRadius: BorderRadius.circular(8),
          color: context.dProvider.whiteColor,
        ),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(
              LocalKeys.aboutClient,
              style: context.titleMedium?.bold6,
            ).hp20,
            20.toHeight,
            Divider(
              color: context.dProvider.black8,
              thickness: 2,
              height: 16,
            ),
            Row(
              crossAxisAlignment: CrossAxisAlignment.center,
              children: [
                ChatTileAvatar(
                  placeHolderImage: ImageAssets.avatar,
                  name: client.fullName,
                  size: 44.0,
                  imageUrl: client.profilePhoto,
                ),
                12.toWidth,
                Expanded(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text(
                        client.fullName,
                        style: context.titleSmall?.bold6,
                        maxLines: 1,
                        overflow: TextOverflow.ellipsis,
                      ),
                      if ((client.companyName ?? '').isNotEmpty)
                        Text(
                          client.companyName!,
                          style: context.titleSmall
                              ?.copyWith(color: context.dProvider.black5),
                          maxLines: 1,
                          overflow: TextOverflow.ellipsis,
                        ),
                    ],
                  ),
                ),
              ],
            ).hp20,
            Divider(
              color: context.dProvider.black8,
              thickness: 2,
              height: 16,
            ),
            if (client.averageRating > 0)
              infos(
                context,
                LocalKeys.rating,
                '${client.averageRating.toStringAsFixed(1)} '
                '(${client.reviewCount})',
              ),
            infos(context, LocalKeys.clientTotalJob, client.totalJobsPosted),
          ],
        ),
      );
    });
  }

  Widget infos(BuildContext context, String title, Object desc) {
    return SizedBox(
      height: 40,
      child: Row(
        children: [
          Expanded(
              child: Text(
            title,
            style: context.titleSmall?.black5,
          )),
          Expanded(
              child: Text(
            desc.toString(),
            textAlign: TextAlign.end,
            style: context.titleSmall?.bold6,
          ))
        ],
      ),
    ).hp20;
  }
}
