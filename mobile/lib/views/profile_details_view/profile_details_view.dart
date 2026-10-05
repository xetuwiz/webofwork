import 'package:flutter/material.dart';
import 'package:provider/provider.dart';
import 'package:webofwork/helper/extension/int_extension.dart';
import 'package:webofwork/helper/local_keys.g.dart';
import 'package:webofwork/services/profile_details_service.dart';
import 'package:webofwork/utils/components/custom_future_widget.dart';
import 'package:webofwork/utils/components/custom_refresh_indicator.dart';
import 'package:webofwork/utils/components/empty_widget.dart';
import 'package:webofwork/utils/components/navigation_pop_icon.dart';
import 'package:webofwork/view_models/profile_details_view_model/profile_details_view_model.dart';
import 'package:webofwork/views/profile_details_view/components/profile_details_basic_info.dart';
import 'package:webofwork/views/profile_details_view/components/profile_details_packages.dart';
import 'package:webofwork/views/profile_details_view/components/profile_details_portfolio.dart';
import 'package:webofwork/views/profile_details_view/components/profile_details_skeleton.dart';
import 'package:webofwork/views/profile_details_view/components/profile_details_skills.dart';

/// Public view of the signed-in freelancer's profile.
///
/// The emptiness gate was `pd.profileDetails.user == null`. This API's payload
/// is flat and has no nested `user` object, so that condition was permanently
/// true and the screen showed `EmptyWidget(infoNotFound)` every time -- it never
/// rendered a thing. `isPopulated` replaces it and checks the fields that
/// actually exist.
class ProfileDetailsView extends StatelessWidget {
  static const routeName = 'profile_details_view';
  const ProfileDetailsView({super.key});

  @override
  Widget build(BuildContext context) {
    final pdm = ProfileDetailsViewModel.instance;
    final pdProvider = Provider.of<ProfileDetailsService>(context, listen: false);
    return Scaffold(
      appBar: AppBar(
        leading: const NavigationPopIcon(),
        title: Text(LocalKeys.profile),
      ),
      body: CustomRefreshIndicator(
        onRefresh: pdProvider.fetchProfileDetails,
        child: CustomFutureWidget(
          function: pdProvider.shouldAutoFetch ? pdProvider.fetchProfileDetails : null,
          shimmer: const ProfileDetailsSkeleton(),
          child: Consumer<ProfileDetailsService>(builder: (context, pd, child) {
            if (!pd.profileDetails.isPopulated) {
              return EmptyWidget(title: LocalKeys.infoNotFound);
            }
            return Scrollbar(
              controller: pdm.controller,
              child: ListView(
                controller: pdm.controller,
                padding: const EdgeInsets.all(20),
                children: [
                  const ProfileDetailsBasicInfo(),
                  20.toHeight,
                  ProfileDetailsPackages(pd: pd),
                  ProfileDetailsSkills(pd: pd),
                  ProfileDetailsPortfolio(pd: pd),
                ],
              ),
            );
          }),
        ),
      ),
    );
  }
}
