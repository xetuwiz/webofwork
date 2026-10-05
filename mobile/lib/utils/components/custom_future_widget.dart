import 'package:flutter/material.dart';

/// Wraps [child] in a [FutureBuilder] that starts [function] after the first
/// frame.
///
/// The previous version took an already-created `Future`, which forced every
/// call site to write `function: service.shouldAutoFetch ? service.fetch() : null`
/// -- the fetch was *invoked during build()*. When the service's first
/// `notifyListeners()` ran synchronously (before the first await), it marked
/// the enclosing `Consumer` dirty mid-build: "setState() or markNeedsBuild()
/// called during build", followed by an infinite rebuild→fetch→notify loop
/// (MyOffers fired dozens of GETs per second until the user left the screen).
///
/// Taking a callback and firing it from a post-frame callback in [initState]
/// guarantees the fetch starts after the build phase, so the synchronous
/// notify lands on a stable tree.
class CustomFutureWidget extends StatefulWidget {
  final Widget child;
  final Widget? shimmer;
  final bool? isLoading;
  final Future<void> Function()? function;

  const CustomFutureWidget({
    super.key,
    required this.child,
    this.function,
    this.shimmer,
    this.isLoading,
  });

  @override
  State<CustomFutureWidget> createState() => _CustomFutureWidgetState();
}

class _CustomFutureWidgetState extends State<CustomFutureWidget> {
  Future<void>? _future;

  @override
  void initState() {
    super.initState();
    final fn = widget.function;
    if (fn != null) {
      WidgetsBinding.instance.addPostFrameCallback((_) {
        if (!mounted) return;
        // Block body, not `() => _future = fn()`: setState asserts that its
        // callback returns void, and an arrow function would return the
        // Future from fn() — "setState() callback argument returned a Future" —
        // which left _future null and the screen stuck on the shimmer forever.
        setState(() {
          _future = fn();
        });
      });
    }
  }

  @override
  Widget build(BuildContext context) {
    final future = _future;
    if (future == null) {
      // No callback, or the post-frame hasn't fired yet. Show the shimmer
      // while a fetch is pending so the screen doesn't flash the empty state.
      if (widget.function != null && widget.shimmer != null) {
        return widget.shimmer!;
      }
      return widget.child;
    }
    return FutureBuilder(
      future: future,
      builder: (context, snapshot) {
        if ((snapshot.connectionState == ConnectionState.waiting ||
                widget.isLoading == true) &&
            widget.shimmer != null) {
          return widget.shimmer!;
        }
        return widget.child;
      },
    );
  }
}
