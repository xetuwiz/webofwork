import 'package:flutter/material.dart';

class ContractListScrollModel {
  ScrollController scrollController = ScrollController();

  ContractListScrollModel._init();
  static ContractListScrollModel? _instance;
  static ContractListScrollModel get instance {
    _instance ??= ContractListScrollModel._init();
    return _instance!;
  }

  tryLoadNextPage(BuildContext context) {}
}
