import 'package:flutter/material.dart';
import 'package:provider/provider.dart';
import 'package:webofwork/helper/extension/string_extension.dart';
import 'package:webofwork/helper/local_keys.g.dart';
import 'package:webofwork/services/project_details_service.dart';
import 'package:webofwork/utils/components/custom_future_widget.dart';
import 'package:webofwork/utils/components/custom_refresh_indicator.dart';
import 'package:webofwork/utils/components/empty_widget.dart';
import 'package:webofwork/views/home_view/components/project_card.dart';

import '/helper/extension/context_extension.dart';
import '/helper/extension/int_extension.dart';
import '/helper/extension/widget_extension.dart';
import '/utils/components/navigation_pop_icon.dart';
import 'components/fixed_price_project_details_buttons.dart';
import 'components/project_details_client_about.dart';
import 'components/project_details_view_skeleton.dart';

/// One open project, with the Apply action and the client's "about" card.
///
/// Reads `GET /api/projects/{id}/details`, a **bare** object. The template
/// expected a `job_details` wrapper, so `projectDetails` was always null and this
/// screen showed `jobDetailsNotFound` for every project. See
/// `models/job_details_model.dart`.
class FixedPriceProjectDetailsView extends StatelessWidget {
  static const routeName = 'fixed_price_project_details_view';
  const FixedPriceProjectDetailsView({super.key});

  @override
  Widget build(BuildContext context) {
    final jdProvider = Provider.of<ProjectDetailsService>(context, listen: false);
    final arguments = ModalRoute.of(context)?.settings.arguments as List;
    final projectId = arguments[0];
    final jobTitle = arguments[1];
    return Scaffold(
        appBar: AppBar(leading: const NavigationPopIcon(), title: Text(jobTitle)),
        body: CustomRefreshIndicator(
          onRefresh: () async {
            await jdProvider.fetchOrderDetails(projectId: projectId, refresh: true);
          },
          child: CustomFutureWidget(
              function: jdProvider.shouldAutoFetch(projectId)
                  ? () => jdProvider.fetchOrderDetails(projectId: projectId)
                  : null,
              shimmer: const ProjectDetailsSkeleton(),
              child: Consumer<ProjectDetailsService>(builder: (context, jd, child) {
                final project = jd.projectDetailsModel.projectDetails;
                if (project == null) {
                  return EmptyWidget(title: LocalKeys.jobDetailsNotFound);
                }

                return ListView(
                  padding: const EdgeInsets.all(20),
                  children: [
                    Container(
                      padding: const EdgeInsets.symmetric(vertical: 20),
                      decoration: BoxDecoration(
                        borderRadius: BorderRadius.circular(8),
                        color: context.dProvider.whiteColor,
                      ),
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          ProjectCard(
                            id: project.id,
                            title: project.title,
                            isFavorite: false,
                            createDate: project.createdAt,
                            // `level` was a template field; this API has no
                            // experience level on a project. The category name
                            // is the real classification it does send.
                            expertise: project.categoryName.capitalize,
                            price: project.displayPrice,
                            priceType: project.pricingType.capitalize,
                            summery: project.description,
                            tags: project.skills,
                            // There is no location on this payload. The card
                            // used to render a hard-coded "----"; passing null
                            // lets it omit the row instead of printing a lie.
                            location: null,
                            // A real count here -- `ProjectsController.Details`
                            // computes it -- so a genuine 0 is shown, unlike the
                            // find-jobs list where the endpoint sends no count.
                            proposalCount: project.proposalsCount,
                            // Nothing reports a client's verification state.
                            userVerified: null,
                            deadline: project.deliveryDays == 0
                                ? null
                                : '${project.deliveryDays} days',
                            // Makes the card non-tappable (it is already the
                            // open page) and renders the full description as
                            // HTML rather than clipping it.
                            fromDetails: true,
                          ),
                          const FixedPriceProjectDetailsButtons().hp20
                        ],
                      ),
                    ),
                    20.toHeight,
                    if (project.requirements.isNotEmpty ||
                        project.expectedDeliverables.isNotEmpty) ...[
                      ProjectDetailsBriefCard(
                        title: LocalKeys.requirements,
                        body: project.requirements,
                      ).hp20,
                      20.toHeight,
                      ProjectDetailsBriefCard(
                        title: LocalKeys.expectedDeliverables,
                        body: project.expectedDeliverables,
                      ).hp20,
                      20.toHeight,
                    ],
                    const ProjectDetailsClientAbout(),
                  ],
                );
              })),
        ));
  }
}

/// Renders one of the project's free-text brief sections.
///
/// `ProjectsController.Details` returns `requirements` and
/// `expected_deliverables` as plain strings. The template had no view for
/// either, so both were fetched and then dropped on the floor.
class ProjectDetailsBriefCard extends StatelessWidget {
  const ProjectDetailsBriefCard({
    super.key,
    required this.title,
    required this.body,
  });

  final String title;
  final String body;

  @override
  Widget build(BuildContext context) {
    if (body.trim().isEmpty) return const SizedBox.shrink();
    return Container(
      width: double.infinity,
      padding: const EdgeInsets.symmetric(vertical: 20),
      decoration: BoxDecoration(
        borderRadius: BorderRadius.circular(8),
        color: context.dProvider.whiteColor,
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(title, style: context.titleMedium?.bold6).hp20,
          16.toHeight,
          Text(
            body,
            style: context.titleSmall?.copyWith(color: context.dProvider.black5),
          ).hp20,
        ],
      ),
    );
  }
}
