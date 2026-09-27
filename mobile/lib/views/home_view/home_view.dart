import 'package:flutter/material.dart';
import 'package:provider/provider.dart';
import 'package:webofwork/helper/extension/context_extension.dart';
import 'package:webofwork/services/project_list_service.dart';
import 'package:webofwork/views/home_view/components/projects_search_field.dart';
import '../../view_models/home_view_model/home_view_model.dart';
import '/utils/components/custom_refresh_indicator.dart';

import '../home_view/components/home_app_bar.dart';
import 'components/active_filter_chips.dart';
import 'components/project_list.dart';

class HomeView extends StatelessWidget {
  const HomeView({super.key});

  @override
  Widget build(BuildContext context) {
    final hvm = HomeViewModel.instance;
    // No count fetch here: it was a side effect in build() that hit two
    // non-existent count routes on every rebuild. Counts are pushed in by the
    // notifications and chat list services instead.
    return Column(
      children: [
        const HomeAppBar(),
        Expanded(
          child: CustomRefreshIndicator(
            onRefresh: () async {
              await Provider.of<ProjectListService>(context, listen: false)
                  .fetchProjectList(refreshing: true);
            },
            child: Container(
              color: context.dProvider.black9,
              child: Column(
                children: [
                  const ProjectsSearchField(),
                  const ActiveFilterChips(),
                  ProjectList(
                    hvm: hvm,
                  ),
                ],
              ),
            ),
          ),
        ),
      ],
    );
  }
}
