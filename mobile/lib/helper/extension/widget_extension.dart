import 'package:flutter/material.dart';
import 'package:flutter_animate/flutter_animate.dart';

extension WidgetExtension on Widget {
  Widget get toSliver {
    return SliverToBoxAdapter(child: this);
  }
}

extension CreateShimmerExtension on Widget {
  Widget get shim {
    return animate(
      delay: 0.ms,
      autoPlay: true,
      onPlay: (controller) => controller.repeat(),
    ).shimmer(
      duration: const Duration(seconds: 1),
      color: Colors.white,
    );
  }
}

extension PaddingExtension on Widget {
  /// Symmetric horizontal padding by [value] logical pixels.
  ///
  /// [hp20] below is the named shorthand for the app's standard 20dp gutter;
  /// nested rows that sit inside an already-padded card (icons, metadata under a
  /// title) need the extra indent that a bare `Padding` call would spell out
  /// every time.
  Widget hp(double value) {
    return Padding(
      padding: EdgeInsets.symmetric(horizontal: value),
      child: this,
    );
  }

  Widget get hp20 => hp(20);

  /// Indent for content aligned under a leading icon in a list row.
  Widget get hp32 => hp(32);
}
