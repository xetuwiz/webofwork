import 'package:flutter_test/flutter_test.dart';
import 'package:webofwork/models/profile_info_model.dart';

/// Regression cover for the profile model.
///
/// The model used to expect a single `{ data: { first_name, ... } }` envelope
/// that this backend never returns, so `data` was always null. That left the
/// Contracts, Profile, Chat and Notifications screens stuck on their loading
/// skeletons and made the dashboard greet the user as "Freelancer".
void main() {
  // Verbatim shape of GET /api/auth/me/
  const authMe = {
    'user': {
      'id': '524dd823-1e08-4894-b214-84903ef0ac36',
      'full_name': 'Xetuwiz Catacolit',
      'email': 'xetuwiz@gmail.com',
      'profile_photo': null,
      'is_freelancer': true,
      'current_role': 'freelancer',
    }
  };

  // Verbatim shape of GET /api/profile/freelancer/me/ -- note: no name, no
  // email, no photo. That is the whole reason the two must be merged.
  const freelancerProfile = {
    'id': '657797ec-07d3-4205-94ed-ea256d7b958a',
    'tagline': 'Full-Stack Developer Building Modern Web Applications',
    'bio': 'Full-stack developer specializing in React, .NET.',
    'phone': '0779091086',
    'is_phone_verified': true,
    'category': 1,
    'experience_level': 'intermediate',
    'availability': true,
    'average_rating': '0.00',
    'review_count': 0,
  };

  group('ProfileInfoModel.fromSources', () {
    test('populates data from the two merged sources', () {
      final model = ProfileInfoModel.fromSources(
        Map<String, dynamic>.from(authMe['user'] as Map),
        Map<String, dynamic>.from(freelancerProfile),
      );

      expect(model.data, isNotNull,
          reason: 'data must be non-null or every consumer renders a skeleton');
      expect(model.data!.firstName, 'Xetuwiz');
      expect(model.data!.lastName, 'Catacolit');
      expect(model.data!.email, 'xetuwiz@gmail.com');
      expect(model.data!.phone, '0779091086');
      expect(model.data!.experienceLevel, 'intermediate');
    });

    test('uses the user id, not the freelancer-profile id', () {
      final model = ProfileInfoModel.fromSources(
        Map<String, dynamic>.from(authMe['user'] as Map),
        Map<String, dynamic>.from(freelancerProfile),
      );

      expect(model.data!.id, '524dd823-1e08-4894-b214-84903ef0ac36');
      expect(model.data!.id, isNot('657797ec-07d3-4205-94ed-ea256d7b958a'));
    });

    test('a single-word name leaves lastName null', () {
      final model = ProfileInfoModel.fromSources(
        {'full_name': 'Cher'},
        const {},
      );
      expect(model.data!.firstName, 'Cher');
      expect(model.data!.lastName, isNull);
    });

    test('collapses extra whitespace between name parts', () {
      final model = ProfileInfoModel.fromSources(
        {'full_name': '  Xetuwiz   Catacolit  '},
        const {},
      );
      expect(model.data!.firstName, 'Xetuwiz');
      expect(model.data!.lastName, 'Catacolit');
    });

    test('a partial fetch still yields a usable model', () {
      // Profile endpoint down, auth/me fine -- must not null out the whole model.
      final model = ProfileInfoModel.fromSources(
        Map<String, dynamic>.from(authMe['user'] as Map),
        const {},
      );
      expect(model.data, isNotNull);
      expect(model.data!.firstName, 'Xetuwiz');
      expect(model.data!.phone, isNull);
    });

    test('empty sources produce an empty model rather than throwing', () {
      final model = ProfileInfoModel.fromSources(const {}, const {});
      expect(model.data, isNotNull);
      expect(model.data!.firstName, isNull);
      expect(model.data!.image, isNull);
    });
  });

  group('resolveMediaUrl', () {
    test('passes absolute urls through untouched', () {
      expect(
        resolveMediaUrl('https://res.cloudinary.com/x.jpg'),
        'https://res.cloudinary.com/x.jpg',
      );
    });

    test('prefixes a /media/ path with the api base', () {
      expect(
        resolveMediaUrl('/media/avatars/me.png'),
        endsWith('/media/avatars/me.png'),
      );
    });

    test('prefixes a bare relative path with the media path', () {
      expect(
        resolveMediaUrl('avatars/me.png'),
        endsWith('/media/avatars/me.png'),
      );
    });

    test('returns null for null, empty and whitespace', () {
      expect(resolveMediaUrl(null), isNull);
      expect(resolveMediaUrl(''), isNull);
      expect(resolveMediaUrl('   '), isNull);
    });
  });
}
