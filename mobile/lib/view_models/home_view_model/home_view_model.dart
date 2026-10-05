import 'package:flutter/material.dart';
import 'package:provider/provider.dart';
import 'package:webofwork/services/project_list_service.dart';

class HomeViewModel {
  ScrollController scrollController = ScrollController();

  HomeViewModel._init();
  static HomeViewModel? _instance;
  static HomeViewModel get instance {
    _instance ??= HomeViewModel._init();
    return _instance!;
  }

  static bool get dispose {
    _instance = null;
    return true;
  }

  tryToLoadMore(BuildContext context) {
    try {
      final jlProvider = Provider.of<ProjectListService>(context, listen: false);

      if (scrollController.offset >= scrollController.position.maxScrollExtent &&
          !scrollController.position.outOfRange) {
        // `hasMore`, not a `next` URL: `project-list` reports
        // `{results, count, page, page_size, total_pages}` and sends no `next`,
        // so the old check was permanently false and page 2 was unreachable.
        if (jlProvider.hasMore && !jlProvider.nextPageLoading) {
          jlProvider.fetchNextPage();
        }
      }
    } catch (e) {
      // A scroll callback that runs after the list is torn down throws; the list
      // is already going away, so there is nothing to report.
    }
  }
}
