class DashboardViewModel {
  DashboardViewModel._init();
  static DashboardViewModel? _instance;
  static DashboardViewModel get instance {
    _instance ??= DashboardViewModel._init();
    return _instance!;
  }

  static bool get dispose {
    _instance = null;
    return true;
  }
}
