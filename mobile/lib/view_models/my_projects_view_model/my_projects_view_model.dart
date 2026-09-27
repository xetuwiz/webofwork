import 'package:flutter/material.dart';

class MyProjectsViewModel {
  ScrollController scrollController = ScrollController();
  MyProjectsViewModel._init();
  static MyProjectsViewModel? _instance;
  static MyProjectsViewModel get instance {
    _instance ??= MyProjectsViewModel._init();
    return _instance!;
  }

  static bool get dispose {
    _instance = null;
    return true;
  }
}
