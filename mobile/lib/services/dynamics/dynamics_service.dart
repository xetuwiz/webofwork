import 'package:flutter/material.dart';

import '../../../customizations.dart' as cus;

class DynamicsService with ChangeNotifier {
  bool onceRebuilt = false;

  Color get primaryColor => cus.primaryColor;
  Color get whiteColor => cus.whiteColor;
  Color get blackColor => cus.blackColor;
  Color get hintColor => cus.hintColor;
  Color get borderColor => cus.borderColor;
  Color get secondaryColor => cus.secondaryColor;
  Color get warningColor => cus.warningColor;
  Color get greenColor => cus.greenColor;
  Color get yellowColor => cus.yellowColor;
  Color get gOneColor => cus.gOneColor;
  Color get gTwoColor => cus.gTwoColor;
  List<Color> get chatAvatarBGColors => cus.chatAvatarBGColors;
  List<Color> get statusColors => cus.statusColors;

  bool _noConnection = false;

  String languageSlug = 'en_GB';

  bool currencyRight = false;
  bool textDirectionRight = false;
  String currencySymbol = "\$";
  String currencyCode = "USD";
  get noConnection => _noConnection;
  get appLocal => Locale(languageSlug.substring(0, 2), languageSlug.substring(3));

  List gridColors = [
    const Color(0xffE6F5DD),
    const Color(0xffFEF4EC),
    const Color(0xffF1F0FF),
    const Color(0xffDDF4FF),
  ];

  Color get black9 => const Color(0xffF2F4F7);
  Color get black8 => const Color(0xffEAECF0);
  Color get black7 => const Color(0xffD0D5DD);
  Color get black6 => const Color(0xff98A2B3);
  Color get black5 => const Color(0xff667085);
  Color get black4 => const Color(0xff475467);
  Color get black3 => const Color(0xff344054);
  Color get black2 => const Color(0xff1D2939);

  Color get primary05 => primaryColor.withOpacity(.05);
  Color get primary10 => primaryColor.withOpacity(.10);
  Color get primary20 => primaryColor.withOpacity(.20);
  Color get primary40 => primaryColor.withOpacity(.40);
  Color get primary60 => primaryColor.withOpacity(.60);
  Color get primary70 => primaryColor.withOpacity(.70);
  Color get primary80 => primaryColor.withOpacity(.80);

  setNoConnection(value) {
    if (value == noConnection) {
      return;
    }
    _noConnection = value;
    notifyListeners();
  }

  /// Applies the app's locale and currency, then marks the service ready.
  ///
  /// This used to `GET /api/platform-settings/` first. **There is no such
  /// endpoint.** The only settings route on this API is
  /// `GET/PATCH /api/admin/settings/global` (`PlatformSettingsController`), which
  /// is `[Authorize(Policy = RequireAdmin)]` -- a freelancer app can never call
  /// it, and a client app has no business reading platform settings anyway.
  ///
  /// The 404 was not silent: `returnResponse`'s `default` branch calls
  /// `showValidationErrors`, so **every cold start raised a spurious error toast**
  /// for a fetch whose entire result was discarded -- the field-level fallbacks
  /// immediately below it assign the same values this class already declares as
  /// its field initialisers.
  ///
  /// So the fetch is gone and the values are simply the defaults. The `Future`
  /// and [onceRebuilt] guard are kept: `app.dart` still calls this
  /// (`dProvider.onceRebuilt ? null : dProvider.getColors()`) and rebuilds on the
  /// resulting `notifyListeners()`.
  ///
  /// The [Future.delayed] is not cosmetic: `app.dart` calls this from inside a
  /// `Consumer` builder during the *first* frame, so a synchronous
  /// `notifyListeners()` tripped the "setState() or markNeedsBuild() called
  /// during build" assertion on every cold start (and the scope element was
  /// never marked dirty, so the rebuilt `Consumer` kept the stale future).
  /// Deferring the notification one event-loop turn runs it after the build
  /// phase, when marking the scope dirty schedules a normal rebuild.
  Future<void> getColors() async {
    if (onceRebuilt) return;
    onceRebuilt = true;
    await Future<void>.delayed(Duration.zero);
    notifyListeners();
  }
}
