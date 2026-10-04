import 'package:flutter_test/flutter_test.dart';
import 'package:webofwork/models/freelancer_profile_model.dart';

/// Regression cover for the profile editor's read/write contract.
///
/// The editor used to POST multipart form fields (`first_name`, `last_name`,
/// `level`, `country`, `state`, `city`) to `/api/profile/freelancer/me/`, a
/// route that only declares a GET, so every save was a 405 that persisted
/// nothing. The real endpoint is `PATCH /api/profile/freelancer/me` with a JSON
/// body of `UpdateFreelancerProfileRequest`.
void main() {
  // Verbatim shape of GET /api/profile/freelancer/profile/
  Map profileJson({
    String fullName = 'Xetuwiz Catacolit',
    int? category = 1,
    String experienceLevel = 'intermediate',
    bool availability = true,
    List<String>? skills,
    dynamic starter,
    dynamic pro,
  }) {
    return {
      'full_name': fullName,
      'profile_photo': null,
      'tagline': 'Full-Stack Developer',
      'bio': 'React and .NET.',
      'phone': '0779091086',
      'category': category,
      'experience_level': experienceLevel,
      'availability': availability,
      'skills': skills ?? ['React', '.NET'],
      'packages': {'starter': starter, 'pro': pro},
      'portfolios': <dynamic>[],
      'average_rating': '0.00',
      'review_count': 0,
    };
  }

  group('FreelancerProfile.fromJson', () {
    test('reads the documented payload', () {
      final profile = FreelancerProfile.fromJson(profileJson());

      expect(profile.fullName, 'Xetuwiz Catacolit');
      expect(profile.tagline, 'Full-Stack Developer');
      expect(profile.bio, 'React and .NET.');
      expect(profile.phone, '0779091086');
      expect(profile.categoryId, 1);
      expect(profile.experienceLevel, 'intermediate');
      expect(profile.availability, isTrue);
      expect(profile.skills, ['React', '.NET']);
    });

    test('parses a decimal that arrives as a string', () {
      // DecimalAsStringJsonConverter writes every decimal as a string.
      final profile = FreelancerProfile.fromJson(profileJson(
        starter: {
          'id': 'abc',
          'price': '150.00',
          'delivery_days': 3,
          'description': ['line one', 'line two'],
        },
      ));

      expect(profile.starter!.price, 150.0);
      expect(profile.starter!.deliveryDays, 3);
      // Descriptions are stored as one string and read back as a list of lines.
      expect(profile.starter!.description, 'line one\nline two');
    });

    test('tolerates a null package and a numeric price', () {
      final profile = FreelancerProfile.fromJson(profileJson(
        starter: {
          'price': 99.5,
          'delivery_days': 7,
          'description': 'single string',
        },
      ));

      expect(profile.starter!.price, 99.5);
      expect(profile.starter!.description, 'single string');
      expect(profile.pro, isNull);
    });

    test('survives a completely empty object', () {
      final profile = FreelancerProfile.fromJson(const {});

      expect(profile.fullName, isEmpty);
      expect(profile.skills, isEmpty);
      expect(profile.availability, isFalse);
      expect(profile.categoryId, isNull);
    });
  });

  group('FreelancerProfile.experienceLevels', () {
    test('are the three values the API validator accepts', () {
      expect(FreelancerProfile.experienceLevels, ['beginner', 'intermediate', 'expert']);
    });

    test('rejects the old display labels', () {
      // These are what the previous dropdown offered, and none of them bind.
      for (final label in ['junior', 'midLevel', 'senior', 'Junior', '']) {
        expect(FreelancerProfile.isValidExperienceLevel(label), isFalse,
            reason: '"$label" must not be sent as experience_level');
      }
    });

    test('accepts each valid level', () {
      for (final level in FreelancerProfile.experienceLevels) {
        expect(FreelancerProfile.isValidExperienceLevel(level), isTrue);
      }
    });
  });

  group('toUpdatePayload', () {
    test('emits only fields the DTO declares', () {
      final payload = FreelancerProfile.fromJson(profileJson()).toUpdatePayload();

      expect(
        payload.keys.toSet(),
        {
          'full_name',
          'tagline',
          'bio',
          'availability',
          'experience_level',
          'skills',
          'category_id',
        },
      );
    });

    test('sends price as a string, because the decimal reader calls GetString', () {
      // DecimalAsStringJsonConverter.Read does `reader.GetString()!`, which
      // throws on a JSON number and turns the request into a 400.
      final profile = FreelancerProfile()
        ..fullName = 'Xetuwiz Catacolit'
        ..starter = PackageTier(price: 150, deliveryDays: 3, description: 'a');

      final packages = profile.toUpdatePayload()['packages'] as Map;
      expect((packages['starter'] as Map)['price'], isA<String>());
      expect((packages['starter'] as Map)['price'], '150.00');
    });

    test('omits a package that has no price', () {
      // The server creates a tier on demand, so sending a blank card would
      // leave an empty package behind.
      final profile = FreelancerProfile()
        ..fullName = 'Xetuwiz Catacolit'
        ..starter = PackageTier(price: 0, deliveryDays: 0, description: '');

      expect(profile.toUpdatePayload().containsKey('packages'), isFalse);
    });

    test('includes only the tiers that are priced', () {
      final profile = FreelancerProfile()
        ..fullName = 'Xetuwiz Catacolit'
        ..starter = PackageTier(price: 100, deliveryDays: 3)
        ..pro = PackageTier(price: 0);

      final packages = profile.toUpdatePayload()['packages'] as Map;
      expect(packages.containsKey('starter'), isTrue);
      expect(packages.containsKey('pro'), isFalse);
    });

    test('drops an unrecognised experience level instead of 422-ing', () {
      final profile = FreelancerProfile()
        ..fullName = 'Xetuwiz Catacolit'
        ..experienceLevel = 'midLevel';

      expect(profile.toUpdatePayload().containsKey('experience_level'), isFalse);
    });

    test('omits a blank full name so the stored name is not wiped', () {
      // The service applies FullName whenever it is non-null, so an empty
      // string would blank the user's name.
      final payload = (FreelancerProfile()..fullName = '   ').toUpdatePayload();
      expect(payload.containsKey('full_name'), isFalse);
    });

    test('trims the full name', () {
      final payload =
          (FreelancerProfile()..fullName = '  Xetuwiz Catacolit  ').toUpdatePayload();
      expect(payload['full_name'], 'Xetuwiz Catacolit');
    });

    test('always sends the complete skill list, empty included', () {
      // The service replaces the set wholesale, so [] is how the last skill is
      // cleared. Omitting the key would silently keep the old skills.
      final cleared = (FreelancerProfile()..fullName = 'Xetuwiz').toUpdatePayload();
      expect(cleared.containsKey('skills'), isTrue);
      expect(cleared['skills'], isEmpty);

      final set = (FreelancerProfile()
            ..fullName = 'Xetuwiz'
            ..skills = ['React', '.NET'])
          .toUpdatePayload();
      expect(set['skills'], ['React', '.NET']);
    });

    test('de-duplicates skills case-insensitively', () {
      final payload = (FreelancerProfile()
            ..fullName = 'Xetuwiz'
            ..skills = ['React', 'react', '  REACT  ', '.NET'])
          .toUpdatePayload();
      expect(payload['skills'], ['React', '.NET']);
    });

    test('sends availability as a real bool, not a string', () {
      final profile = FreelancerProfile()..fullName = 'Xetuwiz';
      profile.availability = false;
      expect(profile.toUpdatePayload()['availability'], isFalse);
      profile.availability = true;
      expect(profile.toUpdatePayload()['availability'], isTrue);
    });

    test('omits category_id when no category is chosen', () {
      final profile = FreelancerProfile()..fullName = 'Xetuwiz';
      expect(profile.toUpdatePayload().containsKey('category_id'), isFalse);
      profile.categoryId = 3;
      expect(profile.toUpdatePayload()['category_id'], 3);
    });

    test('never sends a name, phone or location key', () {
      // None of these exist on UpdateFreelancerProfileRequest; sending them is
      // what the old client did and they were dropped by the binder.
      final profile = FreelancerProfile.fromJson(profileJson())..phone = '0779091086';
      final payload = profile.toUpdatePayload();

      for (final key in [
        'first_name',
        'last_name',
        'phone',
        'country',
        'state',
        'city',
        'email',
        'profile_photo',
      ]) {
        expect(payload.containsKey(key), isFalse, reason: '"$key" is not on the DTO');
      }
    });
  });

  group('ProfileCategoryOption.listFromResponse', () {
    test('reads the bare array returned by no_pagination=true', () {
      final options = ProfileCategoryOption.listFromResponse([
        {'id': 1, 'name': 'Programming'},
        {'id': 2, 'name': 'Design'},
      ]);

      expect(options.length, 2);
      expect(options.first.id, 1);
      expect(options.first.name, 'Programming');
    });

    test('reads the paginated envelope too', () {
      final options = ProfileCategoryOption.listFromResponse({
        'count': 1,
        'next': null,
        'previous': null,
        'results': [
          {'id': 7, 'name': 'Writing'}
        ],
      });

      expect(options.single.id, 7);
      expect(options.single.name, 'Writing');
    });

    test('skips entries with no usable id or name', () {
      final options = ProfileCategoryOption.listFromResponse([
        {'id': 1, 'name': 'Programming'},
        {'id': null, 'name': 'Broken'},
        {'id': 2, 'name': ''},
        'not an object',
      ]);

      expect(options.length, 1);
      expect(options.single.id, 1);
    });

    test('returns empty for junk rather than throwing', () {
      expect(ProfileCategoryOption.listFromResponse(null), isEmpty);
      expect(ProfileCategoryOption.listFromResponse('nope'), isEmpty);
    });
  });
}
