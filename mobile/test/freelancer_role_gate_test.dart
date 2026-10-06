import 'package:flutter_test/flutter_test.dart';

import 'package:webofwork/models/profile_info_model.dart';

/// Reading the freelancer flag out of `/api/auth/me/`.
///
/// ## Why this is tri-state and not a bool
///
/// This app is freelancer-only. The splash signs out anyone the server says is
/// not a freelancer — but it must *not* sign anyone out when the server did not
/// say, because that is also what a failed request looks like. `fetchProfileInfo`
/// deliberately keeps the previous model when both of its source requests fail,
/// so "no data" reaches this layer routinely.
///
/// A two-state parse collapses those two cases into one, and the failure mode is
/// a freelancer being signed out on a flaky connection. `null` is the whole point:
/// it is the difference between "the server said no" and "we do not know".
void main() {
  group('Data.isFreelancer', () {
    final parse = (Map user) => Data.fromSources(user, const {}).isFreelancer;

    test('reads an explicit true', () {
      expect(parse({'is_freelancer': true}), isTrue);
    });

    test('reads an explicit false', () {
      // The value the splash acts on: sign out and send them to the website.
      expect(parse({'is_freelancer': false}), isFalse);
    });

    test('is null when the field is absent', () {
      // Must NOT be false. A missing field means the server did not say, and a
      // failed `/api/auth/me/` looks exactly like this.
      expect(parse({'id': 'abc'}), isNull);
    });

    test('is null for a non-boolean rather than guessing', () {
      // A string "true" or a 1 is not a confirmed answer. Coercing either would
      // let a shape change in the API decide who gets signed out.
      expect(parse({'is_freelancer': 'true'}), isNull);
      expect(parse({'is_freelancer': 1}), isNull);
      expect(parse({'is_freelancer': null}), isNull);
    });

    test('is null for an empty user object', () {
      expect(parse(const {}), isNull);
    });

    test('null and false are distinguishable, which is the entire point', () {
      // Stated as an assertion so that collapsing the type back to a plain bool
      // -- which would compile -- fails here instead of signing users out.
      expect(parse({'is_freelancer': false}), isNot(parse(const {})));
    });
  });

  group('the rest of the model is unaffected', () {
    test('still merges identity from /api/auth/me/', () {
      final data = Data.fromSources(
        {
          'id': 'user-1',
          'full_name': 'Ada Lovelace',
          'email': 'ada@example.com',
          'is_freelancer': true,
        },
        const {'experience_level': 'expert'},
      );

      expect(data.id, 'user-1');
      expect(data.firstName, 'Ada');
      expect(data.lastName, 'Lovelace');
      expect(data.email, 'ada@example.com');
      expect(data.experienceLevel, 'expert');
    });

    test('still separates user id from freelancer profile fields', () {
      // The profile half carries no name or email; mixing them up was a real bug
      // this model already had a fix for.
      final data = Data.fromSources(
        {'id': 'user-1', 'full_name': 'Ada Lovelace'},
        {'phone': '123', 'experience_level': 'expert'},
      );

      expect(data.id, 'user-1');
      expect(data.phone, '123');
    });
  });
}