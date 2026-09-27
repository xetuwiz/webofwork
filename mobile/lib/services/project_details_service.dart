import 'package:flutter/foundation.dart' show ChangeNotifier, debugPrint, kDebugMode;
import 'package:webofwork/helper/extension/string_extension.dart';

import '../data/network/network_api_services.dart';
import '../helper/app_urls.dart';
import '../helper/constant_helper.dart';
import '../helper/local_keys.g.dart';
import '../models/project_details_model.dart';

/// Loads one project for the job-details screen.
///
/// Reads `GET /api/projects/{id}/details` — the freelancer-shaped endpoint —
/// **not** `GET /api/projects/{id}`. The difference matters: the anonymous
/// `{id}` handler returns `MapToResponse(...)`, which never populates
/// `client_info` (it stays `null`) and carries no `is_applied` or
/// `proposals_count`. `ProjectsController.Details` populates all three and is
/// `[Authorize(Policy = "RequireFreelancer")]`, which is exactly this caller.
class ProjectDetailsService with ChangeNotifier {
  ProjectDetailsModel? _projectDetailsModel;
  ProjectDetailsModel get projectDetailsModel => _projectDetailsModel ?? ProjectDetailsModel();

  String token = '';

  /// Whether the cached row is stale, i.e. a fetch is still needed.
  ///
  /// Compares the **project id** from the parsed payload. The template compared
  /// `projectDetails?.id` against the requested id, but `projectDetails` was always null
  /// (the wrapper key did not exist), so this was always true and every
  /// navigation refetched — masking the broken parse rather than causing it.
  bool shouldAutoFetch(Object? id) =>
      _projectDetailsModel?.projectDetails?.id.toString() != id?.toString() || token.isInvalid;

  Future<void> fetchOrderDetails({required projectId, bool refresh = false}) async {
    if (!refresh) {
      _projectDetailsModel = null;
    }
    token = getToken;
    final url = AppUrls.projectDetailsForFreelancerUrl(projectId);
    final responseData = await NetworkApiServices()
        .getApi(url, LocalKeys.projectDetails, headers: acceptJsonAuthHeader);

    if (kDebugMode) {
      debugPrint('[job details] $url -> ${responseData == null ? 'null' : 'ok'}');
    }

    if (responseData is Map) {
      _projectDetailsModel = ProjectDetailsModel.fromJson(responseData);
    }
    notifyListeners();
  }

  /// Called after a successful proposal submit so the button flips to
  /// "Already proposed" without a refetch.
  void setAlreadyApplied() {
    final current = _projectDetailsModel?.projectDetails;
    if (current == null || current.isApplied) return;
    current.isApplied = true;
    notifyListeners();
  }
}
