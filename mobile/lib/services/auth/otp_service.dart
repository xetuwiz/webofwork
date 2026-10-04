import 'dart:async';

import 'package:flutter/material.dart';

import 'sign_up_service.dart';

/// Resend-countdown state for the registration code.
///
/// This used to also *fetch* the code: it POSTed to `resend-otp` and read
/// `responseData['otp']` off the reply, which the server has never returned --
/// `AuthController.ResendOtp` answers `{message}` only. The app therefore could
/// never display a code, and `EnterOtpView` closed the gap by comparing the
/// user's input against that `null`/`otp` field **on the client**, so a correct
/// code was rejected and a wrong one accepted for any value the server did not
/// object to. Verification is now the server's job, via
/// `SignUpService.verifyOtp`; this class only owns the "wait 30s before we
/// email you again" timer.
class OtpService with ChangeNotifier {
  static const Duration resendCooldown = Duration(seconds: 30);

  bool loadingSendOTP = false;
  DateTime? otpSentTime;

  Timer? _cooldownTicker;

  int get secondsRemaining {
    final sent = otpSentTime;
    if (sent == null) return 0;
    final elapsed = DateTime.now().difference(sent);
    final left = resendCooldown - elapsed;
    return left.isNegative ? 0 : left.inSeconds;
  }

  bool get canResend => !loadingSendOTP && secondsRemaining == 0;

  /// Called the moment the code is known to be on its way -- after
  /// `SignUpService.sendOtp` succeeds, and again after every resend.
  void markSent() {
    otpSentTime = DateTime.now();
    _cooldownTicker?.cancel();
    // Ticks once a second purely to refresh the countdown label. Cancelled in
    // [dispose], so leaving the screen does not leave a timer running.
    _cooldownTicker = Timer.periodic(
      const Duration(seconds: 1),
      (_) => notifyListeners(),
    );
  }

  /// Re-sends the code, keeping the server's rate limit honest by refusing
  /// while the countdown is running.
  Future<bool> resend(String email) async {
    if (!canResend) return false;
    loadingSendOTP = true;
    notifyListeners();
    try {
      return await SignUpService().resendOtp(email: email);
    } finally {
      loadingSendOTP = false;
      if (otpSentTime == null) {
        otpSentTime = DateTime.now();
      } else {
        markSent();
      }
      notifyListeners();
    }
  }

  /// Clears the timer when the provider tears the service down.
  ///
  /// Overrides [ChangeNotifier.dispose], so the `super` call is required --
  /// skipping it leaks the listener list and trips the notifier's own debug
  /// assert on the next `notifyListeners()`.
  @override
  void dispose() {
    _cooldownTicker?.cancel();
    _cooldownTicker = null;
    super.dispose();
  }

  /// Reset for a fresh attempt at a different address.
  void reset() {
    _cooldownTicker?.cancel();
    _cooldownTicker = null;
    otpSentTime = null;
    loadingSendOTP = false;
    notifyListeners();
  }
}
