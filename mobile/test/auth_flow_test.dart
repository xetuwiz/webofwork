import 'package:flutter_test/flutter_test.dart';
import 'package:webofwork/helper/app_urls.dart';
import 'package:webofwork/helper/constant_helper.dart';
import 'package:webofwork/services/app_string_service.dart';
import 'package:webofwork/services/auth/sign_up_service.dart';

void main() {
  setUpAll(() {
    // `LocalKeys.*` resolves through `asProvider`, which `coreInit` normally
    // wires up from a `BuildContext`. With an empty translation map
    // `AppStringService.getString` returns the English const unchanged, so this
    // is the same value production sees for an untranslated key.
    asProvider = AppStringService();
  });

  // The previous version POSTed to `/api/auth/register/`, which does not exist on
  // this API, so every sign-up 404'd. The registration flow is now two
  // server-verified requests. These assertions pin the routes, because a wrong
  // path is the single most expensive regression in this flow: it fails on every
  // attempt, for every user, and only at runtime.
  group('registration routes', () {
    test('send-otp is the step-1 route', () {
      expect(AppUrls.registerSendOtpUrl, endsWith('/api/auth/send-otp/'));
    });

    test('verify-otp is the step-2 route and is the only session issuer', () {
      expect(AppUrls.registerVerifyOtpUrl, endsWith('/api/auth/verify-otp/'));
    });

    test('resend-otp is distinct from send-otp', () {
      expect(AppUrls.resendRegistrationOtpUrl, endsWith('/api/auth/resend-otp/'));
      expect(
        AppUrls.sendOtpUrl,
        AppUrls.resendRegistrationOtpUrl,
        reason: 'sendOtpUrl is the legacy name for the resend route; the step-1 '
            'route is registerSendOtpUrl',
      );
      expect(AppUrls.sendOtpUrl, isNot(AppUrls.registerSendOtpUrl));
    });

    // `AuthService.CreateRegistrationUserAsync` hard-codes `UserRole.Client` for
    // every public signup, and every freelancer route is
    // `[Authorize(Policy = "RequireFreelancer")]`. Without this call the account
    // verifies and then 403s on its own dashboard.
    test('switch-role exists, because a signup starts as a client', () {
      expect(AppUrls.switchRoleUrl, endsWith('/api/profile/switch-role'));
    });

    // `emailVerifyUrl` verifies an *email change* (`VerifyEmailChangeRequest(Otp)`),
    // not registration. Registration verification is `registerVerifyOtpUrl`.
    test('email-verify is the email-change route, not registration', () {
      expect(AppUrls.emailVerifyUrl, endsWith('/api/auth/verify-email/'));
      expect(AppUrls.emailVerifyUrl, isNot(AppUrls.registerVerifyOtpUrl));
    });

    test('forgot-password is its own route, not a reset', () {
      expect(AppUrls.forgotPasswordUrl, endsWith('/api/auth/forgot-password/'));
      expect(AppUrls.resetPasswordUrl, endsWith('/api/auth/reset-password/'));
    });
  });

  group('SignUpService.describeError', () {
    // `send-otp` 400s with a per-field map rather than a single `error` string.
    // The old code looked only for `error`, so "Email already exists" and
    // "This field must be at least 6 characters." both fell through to a generic
    // "something went wrong".
    test('unpacks a per-field 400 from send-otp', () {
      expect(
        SignUpService.describeError({
          'email': ['Email already exists']
        }),
        'Email already exists',
      );
      expect(
        SignUpService.describeError({
          'password': ['This field must be at least 6 characters.'],
        }),
        'This field must be at least 6 characters.',
      );
      expect(
        SignUpService.describeError({
          'full_name': ['Full name is required.'],
          'email': ['Email already exists'],
        }),
        'Full name is required.',
      );
    });

    // `verify-otp` uses the flat shape.
    test('unpacks the flat error from verify-otp', () {
      expect(
        SignUpService.describeError({'error': 'Invalid OTP'}),
        'Invalid OTP',
      );
      expect(
        SignUpService.describeError({
          'error': 'Registration session expired. Please register again.',
        }),
        'Registration session expired. Please register again.',
      );
    });

    // `resend-otp` 400s with `non_field_errors` when the 10-minute Redis record
    // is gone. That message is the only signal that the user must restart.
    test('unpacks non_field_errors from resend-otp', () {
      expect(
        SignUpService.describeError({
          'non_field_errors': ['Registration expired. Please register again.'],
        }),
        'Registration expired. Please register again.',
      );
    });

    // The 422 path nests the per-field map under `errors`.
    test('unpacks the 422 shape nested under errors', () {
      expect(
        SignUpService.describeError({
          'errors': {
            'email': ['This field is required.']
          },
        }),
        'This field is required.',
        reason: 'the server sends a non-empty 200 message for send-otp, so a '
            'success body must not be misread as an error',
      );
    });

    test('a 200 message body is returned as-is, not treated as an error', () {
      expect(
        SignUpService.describeError({'message': 'OTP sent to your email'}),
        'OTP sent to your email',
      );
    });

    test('falls back to a generic sentence rather than throwing', () {
      expect(SignUpService.describeError(null), isNotEmpty);
      expect(SignUpService.describeError(42), isNotEmpty);
      expect(SignUpService.describeError(<String, dynamic>{}), isNotEmpty);
      expect(SignUpService.describeError({'email': <String>[]}), isNotEmpty);
    });

    test('passes a plain string body through untouched', () {
      expect(SignUpService.describeError('Invalid OTP'), 'Invalid OTP');
    });
  });
}
