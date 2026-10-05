import 'dart:io';

import 'package:flutter/material.dart';
import 'package:http/http.dart' as http;
import 'package:http_parser/http_parser.dart' show MediaType;
import 'package:provider/provider.dart';
import 'package:webofwork/data/network/network_api_services.dart';
import 'package:webofwork/helper/app_urls.dart';
import 'package:webofwork/helper/constant_helper.dart';
import 'package:webofwork/helper/extension/string_extension.dart';
import 'package:webofwork/helper/local_keys.g.dart';
import 'package:webofwork/models/freelancer_profile_model.dart';
import 'package:webofwork/services/profile_details_service.dart';
import 'package:webofwork/services/profile_info_service.dart';

/// Reads and writes the freelancer's own professional profile.
///
/// The write path used to POST multipart form fields (`first_name`, `last_name`,
/// `level`, `country`, `state`, `city`) to `/api/profile/freelancer/me/`, a
/// route that only has a GET, so every save was a 405 that saved nothing. The
/// real endpoint is `PATCH /api/profile/freelancer/me` with a JSON body, and the
/// old form's field names did not exist on the DTO either.
class ProfileEditService with ChangeNotifier {
  /// Loads the editable profile. Returns null when the request failed; the
  /// network layer has already surfaced the error.
  Future<FreelancerProfile?> fetchProfile() async {
    final response = await NetworkApiServices().getApi(
      AppUrls.fullProfileInfoUrl,
      LocalKeys.profileDetails,
      headers: acceptJsonAuthHeader,
    );

    if (response is! Map) return null;
    return FreelancerProfile.fromJson(response);
  }

  Future<List<ProfileCategoryOption>> fetchCategories() async {
    final response = await NetworkApiServices().getApi(
      AppUrls.allCategoriesUrl,
      LocalKeys.category,
      headers: commonAuthHeader,
    );

    final options = ProfileCategoryOption.listFromResponse(response);
    debugPrint('[ProfileEdit] ${options.length} categories loaded');
    return options;
  }

  /// Saves [profile]. Returns true only when the server actually accepted the
  /// write.
  ///
  /// On success the shared [ProfileInfoService] is refetched too, otherwise the
  /// name shown in the app bar and on the dashboard would keep showing the old
  /// value until the next cold start.
  Future<bool> tryUpdateProfile(
    BuildContext context,
    FreelancerProfile profile,
  ) async {
    final payload = profile.toUpdatePayload();

    if (payload.isEmpty) {
      LocalKeys.nothingToChange.showToast();
      return false;
    }

    debugPrint('[ProfileEdit] PATCH payload: $payload');

    // Resolved before the request so the context is not used across an async
    // gap -- the screen can be popped while the PATCH is in flight.
    final profileInfo = Provider.of<ProfileInfoService>(context, listen: false);

    final response = await NetworkApiServices().patchApi(
      payload,
      AppUrls.freelancerProfileUpdateUrl,
      LocalKeys.editProfile,
      headers: acceptJsonAuthHeader,
    );

    if (!_isSuccess(response)) return false;

    // Refetched so the name in the app bar and on the dashboard is not stale
    // until the next cold start.
    await profileInfo.fetchProfileInfo();

    LocalKeys.profileInfoUpdated.showToast();
    return true;
  }

  // ── Portfolio ────────────────────────────────────────────────────────────────

  /// Uploads a portfolio image and returns the stored URL, or null on failure.
  ///
  /// `POST /api/profile/freelancer/portfolio/upload-image` (multipart) hands the
  /// bytes to the server, which owns the Cloudinary credentials and answers
  /// `{ url }`. The URL is then put into `image_url` by the normal JSON
  /// create/PATCH, so this is upload-only and never writes a row by itself --
  /// picking an image and then abandoning the form leaves nothing behind.
  Future<String?> tryUploadPortfolioImage(File file) async {
    if (!file.existsSync()) return null;

    final request = http.MultipartRequest(
      'POST',
      Uri.parse(AppUrls.portfolioImageUploadUrl),
    )
      // The field name is what the action binds (`[FromForm] IFormFile file`),
      // and the auth header comes from the same source as every other write.
      ..headers.addAll(acceptJsonAuthHeader)
      // Content type is set explicitly from the extension. `fromPath` lets the
      // platform infer it, and on Android that inference produced a non-`image/*`
      // value for a genuine gallery `.jpg` -- which the server rejected as
      // "Only image files are accepted". Being explicit here means the upload
      // does not depend on how the device guessed.
      ..files.add(await http.MultipartFile.fromPath(
        'file',
        file.path,
        contentType: _contentTypeForImage(file.path),
      ));

    final response = await NetworkApiServices().postWithFileApi(
      request,
      LocalKeys.portfolio,
    );

    if (response is Map) {
      final url = response['url']?.toString();
      if (url != null && url.isNotEmpty) return url;
    }
    return null;
  }

  /// Best-effort MIME type for a picked image.
  ///
  /// Only the four extensions the picker offers, so an unknown file falls back to
  /// `application/octet-stream` and is rejected by the server on its extension
  /// check -- rather than being mislabelled as an image.
  static MediaType? _contentTypeForImage(String path) {
    final ext = path.toLowerCase();
    if (ext.endsWith('.jpg') || ext.endsWith('.jpeg')) {
      return MediaType('image', 'jpeg');
    }
    if (ext.endsWith('.png')) return MediaType('image', 'png');
    if (ext.endsWith('.webp')) return MediaType('image', 'webp');
    if (ext.endsWith('.gif')) return MediaType('image', 'gif');
    return null;
  }

  /// Adds a portfolio row: `POST /api/profile/freelancer/portfolio`.
  ///
  /// Portfolio rows are **not** part of `PATCH /api/profile/freelancer/me` --
  /// the DTO has no portfolio field -- so they have their own routes. The server
  /// answers `201` with the created row, so the refetch afterwards is what makes
  /// the new `id` visible to the caller.
  Future<bool> tryCreatePortfolio(
    BuildContext context, {
    required String title,
    String? description,
    String? imageUrl,
  }) async {
    final response = await NetworkApiServices().postApi(
      _portfolioPayload(title, description, imageUrl),
      AppUrls.portfolioUrl,
      LocalKeys.portfolio,
      headers: acceptJsonAuthHeader,
    );
    if (!_isSuccess(response)) return false;
    await _refreshAfterPortfolioWrite(context);
    return true;
  }

  /// `PATCH /api/profile/freelancer/portfolio/{id}` -- the same body shape.
  Future<bool> tryUpdatePortfolio(
    BuildContext context,
    String id, {
    required String title,
    String? description,
    String? imageUrl,
  }) async {
    final response = await NetworkApiServices().patchApi(
      _portfolioPayload(title, description, imageUrl),
      '${AppUrls.portfolioUrl}/$id',
      LocalKeys.edit,
      headers: acceptJsonAuthHeader,
    );
    if (!_isSuccess(response)) return false;
    await _refreshAfterPortfolioWrite(context);
    return true;
  }

  /// `DELETE /api/profile/freelancer/portfolio/{id}`.
  ///
  /// `returnResponse` maps 204/205 to an empty map rather than `null`, so a
  /// null here means a rejected or unreachable delete.
  Future<bool> tryDeletePortfolio(BuildContext context, String id) async {
    final response = await NetworkApiServices().deleteApi(
      '${AppUrls.portfolioUrl}/$id',
      LocalKeys.delete,
      headers: acceptJsonAuthHeader,
    );
    if (response == null) return false;
    await _refreshAfterPortfolioWrite(context);
    return true;
  }

  /// The server validates `title` and `image_url`, so both are always sent and
  /// trimmed -- a blank title is rejected with a 400 rather than stored.
  static Map<String, dynamic> _portfolioPayload(
    String title,
    String? description,
    String? imageUrl,
  ) =>
      {
        'title': title.trim(),
        'description': (description ?? '').trim(),
        'image_url': (imageUrl ?? '').trim(),
      };

  /// Syncs the shared read-only [ProfileDetailsService] after a portfolio write.
  ///
  /// The Profile screen lists the same rows from its own copy, and without this a
  /// portfolio added here stayed invisible there until the next cold start.
  ///
  /// Only that service is refreshed: re-reading the editable profile is the
  /// view model's job (it has to seed [ProfileEditViewModel.portfolios] from the
  /// result anyway), and doing it here as well fired two identical
  /// `GET /api/profile/freelancer/profile/` calls per save.
  Future<void> _refreshAfterPortfolioWrite(BuildContext context) async {
    await Provider.of<ProfileDetailsService>(context, listen: false)
        .fetchProfileDetails();
  }

  /// Keys that only ever appear in a failure body, in any of the shapes this API
  /// produces.
  ///
  /// `title` used to be in this set, which made every portfolio write look like
  /// a failure: `PATCH /portfolio/{id}` answers `Ok(new { id, title, description,
  /// image_url })`, so `containsKey('title')` was true on a 200. The write landed
  /// in the database, but [tryUpdatePortfolio] returned false, the editor sheet
  /// stayed open with no message, and -- because the list was never re-read --
  /// the next save PATCHed the *previous* `image_url` back over the new one.
  ///
  /// So the set is now restricted to keys no success payload in this API uses.
  /// RFC 7807 problem details always carry `status` plus the
  /// `traceId`/`instance` extensions added by ExceptionHandlingMiddleware;
  /// model validation carries `errors`; and a business-rule rejection is
  /// `BadRequest(new { error = "..." })`.
  static const Set<String> _failureKeys = {
    'errors',
    'error',
    'status',
    'detail',
    'traceId',
    'instance',
  };

  /// `returnResponse` hands back the decoded body and discards the status code,
  /// so success has to be inferred from the shape.
  ///
  /// Success is an empty map -- `Ok()` with no body, which is what
  /// `PATCH /api/profile/freelancer/me` answers -- or a payload with none of
  /// [_failureKeys]. Failures come back as the problem-details/validation maps
  /// above, as a JSON array (FluentValidation `BadRequest(validationResult.Errors)`,
  /// which `returnResponse` hands back as a `List`), or as null.
  static bool _isSuccess(dynamic response) {
    if (response is! Map) return false;
    if (response.isEmpty) return true;
    return !response.keys.any((key) => _failureKeys.contains(key.toString()));
  }
}
