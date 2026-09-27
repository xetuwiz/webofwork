import 'package:flutter/material.dart';
import 'package:webofwork/utils/components/app_card.dart';
import 'package:provider/provider.dart';
import 'package:webofwork/helper/extension/context_extension.dart';
import 'package:webofwork/helper/extension/int_extension.dart';
import 'package:webofwork/helper/local_keys.g.dart';
import 'package:webofwork/services/project_list_service.dart';
import 'package:webofwork/view_models/home_drawer_view_model/home_drawer_view_model.dart';

class ProjectsSearchField extends StatelessWidget {
  const ProjectsSearchField({super.key});

  @override
  Widget build(BuildContext context) {
    final hdm = HomeDrawerViewModel.instance;
    final jl = Provider.of<ProjectListService>(context, listen: false);
    return Container(
      margin: const EdgeInsets.only(
        top: 20,
        left: 20,
        right: 20,
      ),
      height: 46,
      decoration: BoxDecoration(
        borderRadius: BorderRadius.circular(AppRadius.control),
        color: context.dProvider.whiteColor,
      ),
      child: TextFormField(
        controller: hdm.searchController,
        decoration: InputDecoration(
          hintText: LocalKeys.searchJob,
          prefix: 6.toWidth,
          suffixIcon: Consumer<ProjectListService>(builder: (context, jl, child) {
            return jl.searchText.isEmpty
                ? const SizedBox()
                : GestureDetector(
                    onTap: () {
                      jl.setSearchText("");
                      hdm.searchController.clear();
                      jl.fetchProjectList();
                    },
                    child: Container(
                        padding: const EdgeInsets.all(6),
                        decoration: const BoxDecoration(
                          color: Colors.transparent,
                        ),
                        child: Icon(
                          Icons.close,
                          color: context.dProvider.black5,
                        )),
                  );
          }),
        ),
        onFieldSubmitted: (value) {
          jl.setSearchText(hdm.searchController.text);
          jl.fetchProjectList();
        },
      ),
    );
  }
}
