import 'package:flutter/material.dart';
import 'package:webofwork/helper/constant_helper.dart';
import 'package:webofwork/helper/extension/string_extension.dart';
import 'package:webofwork/helper/local_keys.g.dart';
import 'package:webofwork/helper/query_string.dart';
import 'package:webofwork/models/category_model.dart';
import 'package:webofwork/data/network/network_api_services.dart';
import 'package:webofwork/helper/app_urls.dart';
import 'package:webofwork/models/project_list_model.dart';

class ProjectListService with ChangeNotifier {
  ProjectListModel? _projectListModel;
  ProjectListModel get projectListModel => _projectListModel ?? ProjectListModel();
  List<Project>? projectList;
  var token = "";
  bool isLoading = false;

  Category? category;
  String? length;
  String? maxPrice;
  String? minPrice;
  String searchText = "";

  /// The `skill` filter. Sent as the skill **name**: `ProjectsController
  /// FreelancerList` matches `x.Skill.Name.ToLower() == skill.ToLower()` (or the
  /// project's `CustomName`), so the drawer must store the name. The web client
  /// sends `skill.id` here, which never equals a name and therefore never
  /// filters -- a React bug mirrored nowhere on purpose.
  String? skill;

  /// The filters this endpoint actually supports are `search`, `category`,
  /// `skill`, `min_price`, `max_price`, `delivery_days` (see
  /// `ProjectsController.FreelancerList`). The template also carried a
  /// sub-category, a country and an experience level; WebOfWork has no such
  /// concept on this route, so they are deliberately not tracked here rather
  /// than being collected and then dropped on the floor.

  /// Maps a drawer label to the `delivery_days` value the server binds
  /// (`ExpectedDays <= value`). Everything on this API is days; the old
  /// strip-the-digits parse turned "less than 2 month" into `2` (a wrong,
  /// duplicate bucket) and "less than a month" into nothing.
  static const Map<String, int> deliveryDaysByLabel = {
    'Less than 3 days': 3,
    'Less than 7 days': 7,
    'Less than 14 days': 14,
    'Less than 30 days': 30,
  };

  setFilters({
    String? length,
    String? maxPrice,
    String? minPrice,
    Category? category,
    String? skill,
  }) {
    if (this.length == length &&
        this.category == category &&
        this.maxPrice == maxPrice &&
        this.minPrice == minPrice &&
        this.skill == skill) {
      return;
    }
    this.length = length;
    this.maxPrice = maxPrice;
    this.minPrice = minPrice;
    this.category = category;
    this.skill = skill;
    fetchProjectList();
  }

  bool nextPageLoading = false;

  bool nexLoadingFailed = false;

  bool get shouldAutoFetch => projectList == null || token.isInvalid;

  /// Query string for the WebOfWork job list.
  ///
  /// The template POSTed a Laravel-shaped body ({string, category, subCategory,
  /// country, level, duration, min_price, max_price}) to /job/filter. WebOfWork's
  /// endpoint is a GET over /api/projects/freelancer/project-list and binds:
  ///   category (int id), skill (name), search, min_price, max_price,
  ///   delivery_days, page, page_size
  /// Note `category` is an id, which is also how the web client sends it --
  /// sending a category *name* yields 400 "The value 'X' is not valid".
  String buildProjectQuery({required int page, int pageSize = _pageSize}) {
    final params = <String, String>{'page': '$page', 'page_size': '$pageSize'};
    void put(String key, String? value) {
      if (value != null && value.trim().isNotEmpty) params[key] = value.trim();
    }

    /// Sends a value as a bare decimal literal, or omits the filter entirely.
    ///
    /// `FreelancerList` binds `min_price` / `max_price` to `decimal?`, so
    /// anything that is not a valid .NET decimal makes model binding fail and
    /// the **whole list request 400s** -- one stray character in the budget
    /// field and Find Projects comes back empty, with no clue why.
    ///
    /// `TextInputType.number` does not prevent this: many locales let the pad
    /// emit `,` as the decimal separator, and a paste of "$1,500" or "500 USD"
    /// gets through too. So the value is normalised here rather than trusting
    /// the keyboard. See [parseBudgetAmount] for the rules.
    void putDecimal(String key, String? value) {
      final parsed = parseBudgetAmount(value);
      if (parsed != null) params[key] = '$parsed';
    }

    put('search', searchText);
    if (category != null) put('category', category?.id.toString());
    put('skill', skill);
    // WebOfWork has no country / experience-level / subcategory concept on
    // this endpoint, so those filters are intentionally not sent.
    putDecimal('min_price', minPrice);
    putDecimal('max_price', maxPrice);
    if (length != null) {
      // Explicit label -> days map, matching the back-end's
      // `ExpectedDays <= delivery_days`. The old regex strip parsed labels like
      // "less than 2 month" to the wrong bucket or to nothing at all.
      final days = deliveryDaysByLabel[length];
      if (days != null) put('delivery_days', '$days');
    }
    return buildQueryString(params);
  }

  /// Page size for the job list. The server clamps this to 1..100.
  static const int _pageSize = 20;

  /// The page currently held, and the server's total for the whole query.
  ///
  /// `ProjectsController.FreelancerList` answers
  /// `{ results, count, page, page_size, total_pages }` -- there is **no `next`
  /// URL**. The template read `json["next"]`, so `nextPage` was permanently null
  /// and [fetchNextPage] bailed out on its first line. Find Projects could therefore
  /// only ever show the first 20 open projects, no matter how many existed.
  /// Paging is therefore driven off `page` / `count` here, and [hasMore] is what
  /// the list view gates its trailing spinner on (same convention as
  /// `MyContractsListService` and `TicketListService`).
  int _page = 1;
  int totalCount = 0;

  /// True when the server has more rows beyond the ones loaded.
  bool get hasMore => _page * _pageSize < totalCount;

  fetchProjectList({refreshing = false}) async {
    token = getToken;
    if (!refreshing) {
      isLoading = true;
      notifyListeners();
    }
    final url = '${AppUrls.projectListUrl}?${buildProjectQuery(page: 1)}';
    final responseData = await NetworkApiServices()
        .getApi(url, LocalKeys.jobList, headers: acceptJsonAuthHeader);

    if (responseData != null) {
      _projectListModel = ProjectListModel.fromJson(responseData);
      final page = _projectListModel!.projects;
      // Replace, not append: a refresh or a filter change must not duplicate rows.
      projectList = page?.projects ?? [];
      _page = 1;
      totalCount = page?.total ?? projectList!.length;
    } else {
      projectList = [];
      _page = 1;
      totalCount = 0;
    }
    isLoading = false;
    notifyListeners();
  }

  fetchNextPage() async {
    token = getToken;
    if (nextPageLoading) return;
    if (!hasMore) return;
    nextPageLoading = true;

    final next = _page + 1;
    final url = '${AppUrls.projectListUrl}?${buildProjectQuery(page: next)}';
    final responseData = await NetworkApiServices()
        .getApi(url, LocalKeys.jobList, headers: commonAuthHeader);

    if (responseData != null) {
      final tempData = ProjectListModel.fromJson(responseData);
      tempData.projects?.projects?.forEach((element) {
        projectList?.add(element);
      });
      _page = next;
      totalCount = tempData.projects?.total ?? totalCount;
    } else {
      nexLoadingFailed = true;
      Future.delayed(const Duration(seconds: 1)).then((value) {
        nexLoadingFailed = false;
        notifyListeners();
      });
    }
    nextPageLoading = false;
    notifyListeners();
  }

  void resetFilters() {
    length = null;
    maxPrice = null;
    minPrice = null;
    category = null;
    skill = null;
    searchText = "";
    _page = 1;
    fetchProjectList();
  }

  void setSearchText(String value) {
    searchText = value;
  }

  // ── Per-filter clears (active-filter chips) ─────────────────────────────────
  // Each chip's dismiss path clears exactly one filter and refetches, instead
  // of wiping every filter the way resetFilters does.

  void clearSkill() {
    if (skill == null) return;
    skill = null;
    _page = 1;
    fetchProjectList();
  }

  void clearCategoryFilter() {
    if (category == null) return;
    category = null;
    _page = 1;
    fetchProjectList();
  }

  void clearPriceRange() {
    if (minPrice == null && maxPrice == null) return;
    minPrice = null;
    maxPrice = null;
    _page = 1;
    fetchProjectList();
  }

  void clearLengthFilter() {
    if (length == null) return;
    length = null;
    _page = 1;
    fetchProjectList();
  }

  void clearSearchFilter() {
    if (searchText.isEmpty) return;
    searchText = "";
    _page = 1;
    fetchProjectList();
  }

  /// Any filter -- other than the free-text search -- currently applied.
  bool get hasActiveFilters =>
      category != null ||
      skill != null ||
      minPrice != null ||
      maxPrice != null ||
      length != null;
}

/// Normalises a typed budget into a decimal .NET can bind, or returns null when
/// there is no usable number in it.
///
/// ## Why the parse is this careful
///
/// `ProjectsController.FreelancerList` declares `min_price` and `max_price` as
/// `decimal?`. A value that is not a valid .NET decimal is a **model-binding
/// failure, which ASP.NET turns into a 400 for the entire request** -- not a
/// silently ignored filter. So one bad character in the budget field empties the
/// whole Find Projects list, with a 200-shaped failure on screen and no indication
/// of why.
///
/// The drawer uses `TextInputType.number`, which does not prevent any of the
/// cases below: a locale keyboard can emit `,` as the decimal separator, and a
/// paste can bring in a currency symbol or a unit.
///
/// ## The rules
///
///  * **`.` is always a decimal point.** The server binds with the invariant
///    culture, and an English-locale user typing `99.95` means ninety-nine
///    ninety-five. Treating `1.500` as fifteen hundred would silently apply the
///    wrong filter, so a dot means a dot.
///  * **`,` is a thousands separator when exactly 3 digits follow it** (`1,500`
///    -> 1500), and a decimal point otherwise (`99,95` -> 99.95). That is the
///    only disambiguation available, and it is the conventional one.
///  * **Currency symbols, spaces and unit letters are stripped**, so `" $1,500 "`
///    and `"1500 USD"` both yield 1500.
///  * **A leading `-` rejects the value outright.** Dropping the sign would turn
///    `-100` into `100` -- applying a *different* filter than the one typed --
///    and a negative price is not a meaningful lower bound for a job budget.
///  * **An incomplete value is dropped, not repaired.** `"."` and `"  -  "` parse
///    to nothing, so the filter is omitted and the list still loads. Sending
///    `min_price=.` would itself 400 the request.
double? parseBudgetAmount(String? input) {
  if (input == null) return null;
  final trimmed = input.trim();
  if (trimmed.isEmpty || trimmed.startsWith('-')) return null;

  // First number-shaped run, keeping separators so they can be classified below.
  final match = RegExp(r'[\d.,]+').firstMatch(trimmed);
  if (match == null) return null;
  final token = match.group(0)!;

  String normalised;
  if (token.contains(',')) {
    // A comma is a **thousands** separator when the run after it is exactly
    // three digits, optionally followed by a decimal part -- so "1,500",
    // "1,234,567" and the combined "1,500.50" all qualify. Otherwise it is a
    // **decimal** comma -- "99,95", "1,5".
    //
    // That is the only disambiguation available from the digits alone, and it
    // is the conventional one. Assuming a thousands comma merely because a dot
    // was also present would turn "1,500.50" into either "150050" or "1.50050",
    // and neither is a price.
    final groups = token.split(',');
    final isThousands = groups.length >= 2 &&
        RegExp(r'^\d{1,3}$').hasMatch(groups.first) &&
        RegExp(r'^\d{3}(\.\d+)?$').hasMatch(groups.last);
    normalised = isThousands ? token.replaceAll(',', '') : token.replaceFirst(',', '.');
  } else {
    // Dots pass through as decimal points -- see the doc comment.
    normalised = token;
  }

  // A second separator means the input was not a number to begin with
  // ("1.2.3"), and guessing which one was intended is not worth a wrong filter.
  if (RegExp(r'[.,].*[.,]').hasMatch(normalised)) return null;

  final parsed = double.tryParse(normalised);
  if (parsed == null || !parsed.isFinite || parsed < 0) return null;
  return parsed;
}
