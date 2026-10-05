// Only `ChangeNotifier`/`kDebugMode` are needed, and importing all of
// `foundation.dart` unqualified would make this file's `Category` model clash with
// Flutter's own `Category` *annotation*.
import 'package:flutter/foundation.dart' show ChangeNotifier, debugPrint;

import '../../data/network/network_api_services.dart';
import '../../helper/app_urls.dart';
import '../../helper/constant_helper.dart';
import '../../helper/local_keys.g.dart';
import '../../models/category_model.dart';

/// Backs the category picker in the job-filter drawer.
///
/// The template version paged this list from the server and searched it with a
/// `?category=` query parameter. Neither worked: `CategoriesController.List` binds
/// only `search` / `no_pagination` / `page` / `page_size`, so `?category=` was
/// silently dropped, and the paginated branch it *did* take always reported
/// `next = null` after ten rows. The net effect was a dropdown that showed nothing.
///
/// Categories are a small, slow-changing, fully public set, so this now asks for
/// the whole list once (`?no_pagination=true`, the same thing the web client's
/// `categoryApi.js` does) and filters it in memory. Search is therefore instant
/// and needs no debounce timer, and there is no "load more" spinner to hang off a
/// `next` URL that will never arrive.
class CategoryDropdownService with ChangeNotifier {
  bool loading = false;

  /// The whole unfiltered set, kept so a search can be re-run against it without
  /// another round trip.
  List<Category> allCategories = [];

  String searchText = '';

  /// The rows the picker renders: [allCategories] narrowed by [searchText].
  List<Category> get categories {
    final term = searchText.trim().toLowerCase();
    if (term.isEmpty) return allCategories;
    return allCategories
        .where((c) => (c.name ?? '').toLowerCase().contains(term))
        .toList();
  }

  bool get hasLoaded => allCategories.isNotEmpty;

  /// Client-side filter. No request, no debounce: the list is already in memory.
  void search(String value) {
    if (value == searchText) return;
    searchText = value;
    notifyListeners();
  }

  /// Loads the full list unless it is already held.
  ///
  /// [force] re-reads it, which is what the picker's "open" gesture used to do --
  /// it cleared and refetched on every tap, so a filter drawer cost a request each
  /// time even when the categories had not changed.
  Future<void> getCategories({bool force = false}) async {
    if (loading) return;
    if (hasLoaded && !force) return;

    loading = true;
    notifyListeners();

    final responseData = await NetworkApiServices().getApi(
      '${AppUrls.categoryUrl}?no_pagination=true',
      LocalKeys.category,
      headers: commonAuthHeader,
      timeoutSeconds: 60,
    );

    if (responseData != null) {
      allCategories = CategoryModel.fromJson(responseData).data;
      // `kDebugMode` is a const, so the whole call is tree-shaken from release.
      debugPrint('Category dropdown loaded ${allCategories.length} rows');
    }

    loading = false;
    notifyListeners();
  }

  void reset() {
    searchText = '';
    allCategories = [];
    notifyListeners();
  }
}
