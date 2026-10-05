import 'package:flutter/material.dart';
import 'package:provider/provider.dart';
import 'package:webofwork/models/category_model.dart';
import 'package:webofwork/services/project_list_service.dart';

/// State for the job-filter drawer.
///
/// Only the filters `ProjectsController.FreelancerList` actually binds are kept:
/// `search`, `category`, `skill`, `min_price`, `max_price`, `delivery_days`.
///
/// The template also carried a sub-category, a country, an experience level, a
/// job type and a rating. WebOfWork has none of those on this route, so the
/// matching UI was collecting a value and then dropping it on the floor -- the
/// controls looked live and changed nothing. They have been removed rather than
/// left as decoration. (`skill` was added back: the backend binds it, and the
/// web client's skill picker was silently broken because it sent the skill **id**
/// where the server compares **names**.)
class HomeDrawerViewModel {
  /// Held in [TextEditingController]s rather than a [RangeValues] notifier: the
  /// budget filter is a min/max pair of free-text fields, so there is no range to
  /// observe. (The notifier existed but nothing read it, so its rebuilds were
  /// no-ops.)
  TextEditingController searchController = TextEditingController();

  ValueNotifier<String?> selectedLengths = ValueNotifier(null);
  TextEditingController maxPriceController = TextEditingController();
  TextEditingController minPriceController = TextEditingController();

  final ValueNotifier<Category?> selectedCategory = ValueNotifier(null);

  /// Holds the skill **name** -- `ProjectsController.FreelancerList` matches
  /// `x.Skill.Name.ToLower() == skill.ToLower()`, so an id (what the web client
  /// sends) never matches anything.
  final ValueNotifier<String?> selectedSkill = ValueNotifier(null);

  HomeDrawerViewModel._init();
  static HomeDrawerViewModel? _instance;
  static HomeDrawerViewModel get instance {
    _instance ??= HomeDrawerViewModel._init();
    return _instance!;
  }

  static bool get dispose {
    _instance = null;
    return true;
  }

  setFilters(BuildContext context) async {
    final jl = Provider.of<ProjectListService>(context, listen: false);
    jl.setSearchText(searchController.text.trim());
    jl.setFilters(
      length: selectedLengths.value,
      maxPrice: maxPriceController.text,
      minPrice: minPriceController.text,
      category: selectedCategory.value,
      skill: selectedSkill.value,
    );
  }

  /// Clears both the service's query state and this view model's controls.
  ///
  /// These used to be two independent operations: [ProjectListService.resetFilters]
  /// cleared the query but left every dropdown and text field showing its old
  /// value, so pressing "Reset" re-ran a cleared search while the UI claimed the
  /// filters were still applied. Pressing "Apply" straight after would then
  /// re-apply them.
  resetFilters(BuildContext context) {
    selectedCategory.value = null;
    selectedLengths.value = null;
    selectedSkill.value = null;
    searchController.clear();
    minPriceController.clear();
    maxPriceController.clear();

    final jl = Provider.of<ProjectListService>(context, listen: false);
    jl.resetFilters();
  }

  setValues({
    String? length,
    String? maxPrice,
    String? minPrice,
    Category? category,
    String? skill,
  }) {
    selectedCategory.value = category;
    selectedLengths.value = length;
    selectedSkill.value = skill;
    maxPriceController.text = maxPrice ?? "";
    minPriceController.text = minPrice ?? "";
  }
}
