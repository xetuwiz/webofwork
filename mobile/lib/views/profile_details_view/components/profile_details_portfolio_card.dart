import 'package:cached_network_image/cached_network_image.dart';
import 'package:flutter/material.dart';
import 'package:webofwork/helper/extension/context_extension.dart';
import 'package:webofwork/helper/extension/int_extension.dart';
import 'package:webofwork/helper/extension/widget_extension.dart';
import 'package:webofwork/models/profile_details_model.dart';

import '../../../utils/components/image_pl_widget.dart';

/// One portfolio entry.
///
/// The payload key is `image_url` and the value is already resolved to a
/// loadable URL by `resolveMediaUrl`, so there is no `path` to join -- the
/// previous `"$path/${portFolio.image}"` produced a URL like `null/` and the
/// thumbnail never loaded.
class ProfileDetailsPortfolioCard extends StatelessWidget {
  final PortfolioItem item;
  const ProfileDetailsPortfolioCard({super.key, required this.item});

  @override
  Widget build(BuildContext context) {
    final url = item.imageUrl;
    return Row(
      crossAxisAlignment: CrossAxisAlignment.center,
      children: [
        Expanded(
          flex: 5,
          child: SizedBox(
            height: 80,
            child: ClipRRect(
              borderRadius: BorderRadius.circular(8),
              child: (url == null || url.isEmpty)
                  ? const ImagePLWidget(size: 60)
                  : CachedNetworkImage(
                      fit: BoxFit.cover,
                      imageUrl: url,
                      placeholder: (context, url) => const ImagePLWidget(size: 60),
                      errorWidget: (context, url, error) => const ImagePLWidget(size: 60),
                    ),
            ),
          ),
        ),
        const SizedBox(width: 12),
        Expanded(
          flex: 12,
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text(
                item.title ?? "",
                maxLines: 2,
                overflow: TextOverflow.ellipsis,
                style: context.titleMedium?.bold6,
              ),
              if ((item.description ?? '').isNotEmpty) ...[
                8.toHeight,
                Text(
                  item.description!,
                  maxLines: 3,
                  overflow: TextOverflow.ellipsis,
                  style: context.titleSmall?.copyWith(color: context.dProvider.black5),
                ),
              ],
            ],
          ),
        ),
      ],
    ).hp20;
  }
}
