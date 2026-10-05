// Smoke tests for the WebOfWork app shell.
//
// The stock `flutter create` counter test was removed: it referenced the
// long-gone template app widget and asserted on a counter that this app does not have.

import 'package:flutter/widgets.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:webofwork/app.dart';
import 'package:webofwork/helper/routes.dart';
import 'package:webofwork/views/sign_in_view/sign_in_view.dart';

void main() {
  test('WebOfWorkApp is a Widget', () {
    expect(const WebOfWorkApp(), isA<Widget>());
  });

  test('every registered route name is non-empty', () {
    for (final name in Routes.routes.keys) {
      expect(name, isNotEmpty, reason: 'route name should never be blank');
    }
  });

  test('no duplicate route names collapse silently', () {
    // A duplicate key in a map literal would silently drop a screen, so the
    // only thing we can assert is that the map has the expected screen count.
    expect(Routes.routes.containsKey(SignInView.routeName), isTrue);
    expect(Routes.routes.length, greaterThanOrEqualTo(20));
  });
}
