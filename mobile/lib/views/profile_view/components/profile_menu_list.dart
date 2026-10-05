import 'package:flutter/material.dart';
import 'package:provider/provider.dart';
import 'package:webofwork/helper/svg_assets.dart';
import 'package:webofwork/services/account_delete_service.dart';
import 'package:webofwork/view_models/profile_details_view_model/profile_details_view_model.dart';
import 'package:webofwork/views/my_offers_view/my_offers_view.dart';
import 'package:webofwork/views/my_proposals_view/my_proposals_view.dart';
import 'package:webofwork/views/profile_details_view/profile_details_view.dart';
import 'package:webofwork/views/profile_settings_view/profile_settings_view.dart';

import '../../../services/profile_info_service.dart';
import '../../../utils/components/alerts.dart';
import '/helper/extension/context_extension.dart';
import '/helper/local_keys.g.dart';
import 'profile_menu_tile.dart';

class ProfileMenuList extends StatelessWidget {
  const ProfileMenuList({super.key});

  @override
  Widget build(BuildContext context) {
    return Container(
      decoration: BoxDecoration(
        borderRadius: BorderRadius.circular(8),
      ),
      child: Column(
        children: [
          ProfileMenuTile(
            title: LocalKeys.myOffers,
            svg: SvgAssets.clipboardText,
            onPress: () {
              context.toNamed(MyOffersView.routeName);
            },
          ),
          ProfileMenuTile(
            title: LocalKeys.myProposals0,
            svg: SvgAssets.task,
            onPress: () {
              context.toNamed(MyProposalsView.routeName);
            },
          ),
          ProfileMenuTile(
            title: LocalKeys.viewProfile0,
            svg: SvgAssets.userSquare,
            onPress: () {
              context.toNamed(ProfileDetailsView.routeName, then: () {
                ProfileDetailsViewModel.dispose;
              });
            },
          ),
          ProfileMenuTile(
            title: LocalKeys.profileSettings0,
            svg: SvgAssets.setting,
            onPress: () {
              context.toNamed(ProfileSettingsView.routeName);
            },
          ),
          ProfileMenuTile(
            title: LocalKeys.deleteAccount0,
            svg: SvgAssets.trash,
            onPress: () {
              Alerts().confirmationAlert(
                context: context,
                title: LocalKeys.areYouSure,
                buttonText: LocalKeys.delete,
                onConfirm: () async {
                  final deleted = await Provider.of<AccountDeleteService>(
                    context,
                    listen: false,
                  ).tryAccountDelete();

                  // Account deletion is a network round trip and the user can
                  // leave the screen while it is in flight. Touching `context`
                  // afterwards -- `Provider.of` and `popFalse` both walk the
                  // element tree -- throws on a defunct element, which surfaces
                  // as a red screen after a delete that actually succeeded.
                  if (!context.mounted) return;

                  if (deleted == true) {
                    Provider.of<ProfileInfoService>(
                      context,
                      listen: false,
                    ).reset();
                    context.popFalse;
                  }
                },
              );
            },
          ),
        ],
      ),
    );
  }
}
