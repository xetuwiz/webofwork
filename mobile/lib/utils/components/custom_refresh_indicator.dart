import 'package:flutter/material.dart';

import '/helper/extension/context_extension.dart';

/// Wrapper around [RefreshIndicator] with the callback strongly typed.
///
/// The previous version declared both fields `final dynamic`, so every screen
/// passed a raw closure through. `RefreshIndicator.onRefresh` is a
/// `Future<void> Function()`, and Flutter inserts an implicit runtime downcast
/// when the wrapper forwards `onRefresh` — a callback whose body returns
/// `Future<dynamic>` (any `async` method without an explicit return type)
/// then throws `type '() => dynamic' is not a subtype of '() => Future<void>'`
/// and paints the red error banner on that screen. Typing the field makes any
/// mismatch a compile-time error instead.
class CustomRefreshIndicator extends StatelessWidget {
  final Widget? child;
  final Future<void> Function()? onRefresh;
  const CustomRefreshIndicator({super.key, this.child, this.onRefresh});

  @override
  Widget build(BuildContext context) {
    return RefreshIndicator(
        backgroundColor: context.dProvider.primary40,
        color: context.dProvider.whiteColor,
        onRefresh: onRefresh ?? () async {},
        child: child ?? const SizedBox.shrink());
  }
}