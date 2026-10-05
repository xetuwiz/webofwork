import 'package:flutter/foundation.dart' show ChangeNotifier, debugPrint;

import '../../data/network/network_api_services.dart';
import '../../helper/app_urls.dart';
import '../../helper/constant_helper.dart';
import '../../helper/local_keys.g.dart';
import '../../models/skill_model.dart';

/// Backs the skill picker in the job-filter drawer.
///
/// Skills are a small, public, slow-changing set served by
/// `GET /api/categories/skills?no_pagination=true` (a bare JSON array of
/// `{id, name, …}`). As with [CategoryDropdownService], the whole list is asked
/// for once and searched in memory, so there is no paging to walk and no
/// debounce timer.
///
/// The web client's Find Projects screen sends the skill **id** as `?skill=`, but
/// `ProjectsController.FreelancerList` matches skill *names*
/// (`x.Skill.Name.ToLower() == skill.ToLower()`), so every React skill filter
/// silently no-op'd. This picker stores the name, which is what the server
/// actually filters on.
class SkillDropdownService with ChangeNotifier {
  bool loading = false;

  /// The whole unfiltered set, kept so a search can be re-run in memory.
  List<Skill> allSkills = [];

  String searchText = '';

  /// [allSkills] narrowed by [searchText].
  List<Skill> get skills {
    final term = searchText.trim().toLowerCase();
    if (term.isEmpty) return allSkills;
    return allSkills
        .where((s) => s.name.toLowerCase().contains(term))
        .toList();
  }

  bool get hasLoaded => allSkills.isNotEmpty;

  /// Client-side filter. No request, no debounce.
  void search(String value) {
    if (value == searchText) return;
    searchText = value;
    notifyListeners();
  }

  Future<void> getSkills({bool force = false}) async {
    if (loading) return;
    if (hasLoaded && !force) return;

    loading = true;
    notifyListeners();

    final responseData = await NetworkApiServices().getApi(
      AppUrls.skillsUrl,
      LocalKeys.skill,
      headers: commonAuthHeader,
      timeoutSeconds: 60,
    );

    if (responseData is List) {
      allSkills = responseData
          .whereType<Map>()
          .map((e) => Skill.fromJson(Map<String, dynamic>.from(e)))
          .toList();
      debugPrint('Skill dropdown loaded ${allSkills.length} rows');
    }

    loading = false;
    notifyListeners();
  }

  void reset() {
    searchText = '';
    allSkills = [];
    notifyListeners();
  }
}