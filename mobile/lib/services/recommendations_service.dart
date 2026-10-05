import 'package:flutter/material.dart';
import 'package:webofwork/helper/app_urls.dart';
import 'package:webofwork/data/network/network_api_services.dart';
import 'package:webofwork/models/project_list_model.dart'; // Reuse Project model

class RecommendationsService with ChangeNotifier {
  List<Project> _recommendedProjects = [];
  List<Project> get recommendedProjects => _recommendedProjects;

  bool _isLoading = false;
  bool get isLoading => _isLoading;

  final NetworkApiServices _apiService = NetworkApiServices();

  Future<void> fetchRecommendations() async {
    _isLoading = true;
    notifyListeners();

    try {
      final response =
          await _apiService.getApi(AppUrls.recommendedProjectsUrl, 'Fetch Recommendations');

      if (response != null && response is List) {
        _recommendedProjects = response.map((e) {
          final map = Map<String, dynamic>.from(e);
          // Convert camelCase to snake_case if needed so Project.fromJson works
          if (map.containsKey('pricingType')) map['pricing_type'] = map['pricingType'];
          if (map.containsKey('fixedPrice')) map['fixed_price'] = map['fixedPrice'];
          if (map.containsKey('minBudget')) map['min_budget'] = map['minBudget'];
          if (map.containsKey('maxBudget')) map['max_budget'] = map['maxBudget'];
          if (map.containsKey('deliveryDays')) map['delivery_days'] = map['deliveryDays'];
          if (map.containsKey('createdAt')) map['created_at'] = map['createdAt'];
          return Project.fromJson(map);
        }).toList();
      }
    } catch (e) {
      debugPrint("Error fetching recommendations: $e");
    } finally {
      _isLoading = false;
      notifyListeners();
    }
  }
}
