import 'package:flutter/material.dart';
import 'package:provider/provider.dart';
import 'package:webofwork/helper/extension/context_extension.dart';
import 'package:webofwork/helper/extension/int_extension.dart';
import 'package:webofwork/helper/extension/string_extension.dart';
import 'package:webofwork/helper/local_keys.g.dart';
import 'package:webofwork/services/project_details_service.dart';
import 'package:webofwork/services/profile_info_service.dart';
import 'package:webofwork/views/fixed_price_project_details_view/components/apply_project_plain_sheet.dart';

import '../../../helper/svg_assets.dart';
import '../../../view_models/sign_in_view/sign_in_view_model.dart';
import '../../../view_models/sign_up_view/sign_up_view_model.dart';
import '../../sign_in_view/sign_in_view.dart';

/// The sign-in / Apply / Already-proposed row on the job-details screen.
///
/// The disabled state is driven by the real `is_applied` bool from
/// `GET /api/projects/{id}/details`. It used to be
/// `json["check_proposal_send_or_not"].toString() != "0"`, which Dart evaluates
/// as `"null" != "0"` — always true — so **Apply was permanently disabled** with
/// the label "Already proposed" on projects the freelancer had never bid on.
class FixedPriceProjectDetailsButtons extends StatelessWidget {
  const FixedPriceProjectDetailsButtons({
    super.key,
  });

  @override
  Widget build(BuildContext context) {
    return Consumer<ProjectDetailsService>(builder: (context, jd, child) {
      final project = jd.projectDetailsModel.projectDetails;
      return Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          20.toHeight,
          Consumer<ProfileInfoService>(builder: (context, pi, child) {
            return pi.profileInfoModel.data == null
                ? SizedBox(
                    width: double.infinity,
                    child: ElevatedButton.icon(
                        onPressed: () {
                          SignInViewModel.instance.initSavedInfo();
                          SignUpViewModel.dispose;
                          context.toNamed(SignInView.routeName);
                        },
                        icon: SvgAssets.user
                            .toSVGSized(20, color: context.dProvider.whiteColor),
                        label: Text(LocalKeys.signIn)),
                  )
                : SizedBox(
                    width: double.infinity,
                    child: OutlinedButton.icon(
                        onPressed: (project?.isApplied ?? false)
                            ? () {
                                LocalKeys.youCannotApplyToAJobMultipleTimes.showToast();
                              }
                            : () {
                                final projectId = project?.id;
                                if (projectId == null || projectId == 0) {
                                  LocalKeys.requestFailed.showToast();
                                  return;
                                }
                                showModalBottomSheet(
                                  context: context,
                                  backgroundColor: Colors.transparent,
                                  isDismissible: false,
                                  isScrollControlled: true,
                                  builder: (context) => ApplyProjectPlainSheet(
                                    projectId: projectId,
                                  ),
                                );
                              },
                        icon: SvgAssets.send
                            .toSVGSized(24, color: context.dProvider.black6),
                        label: Text((project?.isApplied ?? false)
                            ? LocalKeys.alreadyProposed
                            : LocalKeys.sendProposal)),
                  );
          }),
        ],
      );
    });
  }
}
