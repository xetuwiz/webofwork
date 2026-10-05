import 'package:flutter/material.dart';
import 'package:webofwork/data/network/network_api_services.dart';
import 'package:webofwork/helper/app_urls.dart';
import 'package:webofwork/helper/constant_helper.dart';
import 'package:webofwork/helper/extension/string_extension.dart';

import '../helper/local_keys.g.dart';
import '../models/profile_details_model.dart';

/// Read-only public profile, from `GET /api/profile/freelancer/profile`.
///
/// Two dead write paths were removed from here:
///   * `tryChangingStatus()` posted `check_work_availability` to
///     `profileInfoUrl` -- a GET-only route, so the switch always 405'd and the
///     toggle was pure decoration. Availability is now edited through
///     `PATCH /api/profile/freelancer/me` on the Profile Edit screen.
///   * `tryStatusChange()` posted `project_on_off` to a project route. Clients
///     have no projects on this platform, so the whole catalogue concept is gone.
class ProfileDetailsService with ChangeNotifier {
  ProfileDetailsModel? _profileDetails;
  ProfileDetailsModel get profileDetails => _profileDetails ?? ProfileDetailsModel();
  var token = "";

  bool get shouldAutoFetch => _profileDetails == null || token.isInvalid;

  Future<void> fetchProfileDetails() async {
    token = getToken;
    final responseData = await NetworkApiServices().getApi(
        AppUrls.fullProfileInfoUrl, LocalKeys.profileDetails,
        headers: commonAuthHeader);
    if (responseData is Map) {
      _profileDetails =
          ProfileDetailsModel.fromJson(Map<String, dynamic>.from(responseData));
    }
    notifyListeners();
  }
}
