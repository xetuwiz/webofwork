import 'package:flutter/material.dart';

import '../../data/network/network_api_services.dart';
import '../../helper/app_urls.dart';
import '../../helper/constant_helper.dart';
import '../../models/profile_info_model.dart';

class ProfileInfoService with ChangeNotifier {
  ProfileInfoModel? _profileInfoModel;

  ProfileInfoModel get profileInfoModel => _profileInfoModel ?? ProfileInfoModel();

  /// Fetches the two endpoints that together describe the signed-in user and
  /// merges them into one model.
  ///
  /// Both are required because the data is genuinely split: the freelancer
  /// profile has no name, email or photo, and `/api/auth/me/` has no phone or
  /// experience level.
  ///
  /// `data` is left unset only when *both* requests fail, so one transient
  /// failure cannot strand the Contracts / Profile / Chat / Notifications
  /// screens on their loading skeleton.
  Future fetchProfileInfo() async {
    final api = NetworkApiServices();
    final responses = await Future.wait([
      api.getApi(AppUrls.authMeUrl, 'Fetch Profile',
          headers: acceptJsonAuthHeader, timeoutSeconds: 60),
      api.getApi(AppUrls.profileInfoUrl, 'Fetch Profile',
          headers: acceptJsonAuthHeader, timeoutSeconds: 60),
    ]);

    final user = _userObject(responses[0]);
    final profile = _asMap(responses[1]);

    if (user.isEmpty && profile.isEmpty) {
      // Both failed (offline / server down). Leave the previous value in place
      // so the skeletons keep the screen retryable rather than showing a
      // half-populated profile.
      debugPrint('[Profile] both profile sources unavailable');
      notifyListeners();
      return;
    }

    _profileInfoModel = ProfileInfoModel.fromSources(user, profile);
    debugPrint('[Profile] loaded: ${_profileInfoModel?.data?.firstName} '
        '(${_profileInfoModel?.data?.email})');
    notifyListeners();
  }

  /// `/api/auth/me/` nests the identity under a `user` key.
  static Map<String, dynamic> _userObject(dynamic response) {
    if (response is! Map) return const {};
    final nested = response['user'];
    return nested is Map ? Map<String, dynamic>.from(nested) : const {};
  }

  /// The freelancer profile endpoint returns its object at the top level.
  static Map<String, dynamic> _asMap(dynamic response) =>
      response is Map ? Map<String, dynamic>.from(response) : const {};

  void reset() {
    _profileInfoModel = null;
    notifyListeners();
  }
}
