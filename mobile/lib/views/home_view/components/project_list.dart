import 'package:flutter/material.dart';
import 'package:provider/provider.dart';
import 'package:webofwork/helper/extension/int_extension.dart';
import 'package:webofwork/helper/extension/string_extension.dart';
import 'package:webofwork/helper/extension/widget_extension.dart';
import 'package:webofwork/helper/local_keys.g.dart';
import 'package:webofwork/services/project_list_service.dart';
import 'package:webofwork/utils/components/custom_future_widget.dart';
import 'package:webofwork/utils/components/empty_widget.dart';
import 'package:webofwork/view_models/home_view_model/home_view_model.dart';

import '../../../utils/components/custom_preloader.dart';
import 'project_card.dart';
import 'project_card_skeleton.dart';

class ProjectList extends StatelessWidget {
  final HomeViewModel hvm;
  const ProjectList({super.key, required this.hvm});

  @override
  Widget build(BuildContext context) {
    hvm.scrollController.addListener(() {
      hvm.tryToLoadMore(context);
    });
    return Expanded(child: Consumer<ProjectListService>(builder: (context, jl, child) {
      return CustomFutureWidget(
          function: jl.shouldAutoFetch ? () => jl.fetchProjectList(refreshing: true) : null,
          shimmer: ListView.separated(
              padding: const EdgeInsets.all(20),
              physics: const NeverScrollableScrollPhysics(),
              itemBuilder: (context, index) {
                return const ProjectCardSkeleton(fromDetails: false);
              },
              separatorBuilder: (context, index) => 16.toHeight,
              itemCount: 20).shim,
          isLoading: jl.isLoading,
          child: jl.projectList?.isEmpty ?? true == true
              ? EmptyWidget(title: LocalKeys.noJobFound)
              : Scrollbar(
                  controller: hvm.scrollController,
                  child: ListView.separated(
                      padding: const EdgeInsets.all(20),
                      physics: const AlwaysScrollableScrollPhysics(),
                      controller: hvm.scrollController,
                      itemBuilder: (context, index) {
                        if ((jl.hasMore && !jl.nexLoadingFailed) &&
                            index == jl.projectList!.length) {
                          return const CustomPreloader();
                        }

                        final projectItem = jl.projectList![index];
                        return ProjectCard(
                            id: projectItem.id,
                            title: projectItem.title,
                            isFavorite: false,
                            createDate: projectItem.createdAt,
                            expertise: projectItem.categoryName.toString().capitalize,
                            price: projectItem.budget,
                            priceType: projectItem.pricingType.toString().capitalize,
                            summery: projectItem.description,
                            tags: projectItem.projectSkills.map((e) => e.skill).toList(),
                            // Null, not a placeholder. `GET
                            // /api/projects/freelancer/project-list` sends no
                            // location, so the old
                            // `projectCreator?.userCountry?.country ?? "----"`
                            // invented one from a model this payload never
                            // populates.
                            location: null,
                            // Null, not 0. The endpoint sends no proposal
                            // count, so "Proposals: 0" asserted as fact that
                            // nobody had bid.
                            proposalCount: null,
                            // Null, not false. Nothing on this API reports a
                            // client's verification state, so "Not verified"
                            // was an unfounded claim about a real person.
                            userVerified: null,
                            deadline: projectItem.duration);
                      },
                      separatorBuilder: (context, index) => 16.toHeight,
                      itemCount: jl.projectList!.length +
                          (jl.hasMore && !jl.nexLoadingFailed ? 1 : 0)),
                ));
    }));
  }
}
