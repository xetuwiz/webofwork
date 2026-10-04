// Verifies MyPackages / SubmittedProposal against the payload shape the server
// actually produces for GET /api/profile/freelancer/profile and
// POST /api/projects/create-proposal.
//
// These are the two failure modes that made the apply flow unusable in both
// clients, so they are worth pinning:
//
//  * `price` is a C# `decimal` written by `DecimalAsStringJsonConverter`, so it
//    arrives as a **quoted string** ("250.00"). The old Flutter sheet read it
//    into a `num` and threw a `TypeError` the moment a tier was tapped.
//  * A tier the freelancer has not created is **null**, not absent --
//    `"packages": { "starter": {...}, "pro": null }`. Anything that iterates the
//    object or indexes it without a null check breaks.
import 'package:flutter_test/flutter_test.dart';
import 'package:webofwork/models/proposal_model.dart';

void main() {
  group('MyPackages.fromProfile', () {
    test('reads both tiers out of the tier-keyed object', () {
      final packages = MyPackages.fromProfile({
        'full_name': 'Ada Lovelace',
        'packages': {
          'starter': {
            'id': '2f1c9e40-0000-4000-8000-000000000001',
            'package_type': 'starter',
            'price': '250.00',
            'delivery_days': 7,
            'description': ['Logo design', 'Two revisions'],
          },
          'pro': {
            'id': '2f1c9e40-0000-4000-8000-000000000002',
            'package_type': 'pro',
            'price': '800',
            'delivery_days': 21,
            'description': <String>[],
          },
        },
      });

      expect(packages.isNotEmpty, isTrue);
      expect(packages.all.length, 2);
      // Cheaper tier first, regardless of key order in the payload.
      expect(packages.all.first.packageType, 'starter');
      expect(packages.all.last.packageType, 'pro');
    });

    test('parses the quoted decimal price', () {
      final packages = MyPackages.fromProfile({
        'packages': {
          'starter': {
            'id': 'a',
            'package_type': 'starter',
            'price': '250.00',
            'delivery_days': 7,
          },
          'pro': null,
        },
      });

      expect(packages.defaultTier!.price, 250.0);
    });

    test('tolerates a price sent as a bare number', () {
      final packages = MyPackages.fromProfile({
        'packages': {
          'starter': {'id': 'a', 'price': 250, 'delivery_days': 7},
        },
      });

      expect(packages.defaultTier!.price, 250.0);
    });

    test('a null tier is skipped, not parsed into an empty package', () {
      final packages = MyPackages.fromProfile({
        'packages': {
          'starter': {
            'id': 'a',
            'package_type': 'starter',
            'price': '250.00',
            'delivery_days': 7,
          },
          'pro': null,
        },
      });

      expect(packages.all.length, 1);
      expect(packages.tiers.containsKey('pro'), isFalse);
      // With no starter the single owned tier is the default.
      expect(packages.defaultTier!.id, 'a');
    });

    test('only offers pro when starter is absent', () {
      final packages = MyPackages.fromProfile({
        'packages': {
          'starter': null,
          'pro': {'id': 'b', 'package_type': 'pro', 'price': '10', 'delivery_days': 1},
        },
      });

      expect(packages.defaultTier!.id, 'b');
      expect(packages.defaultTier!.label, 'Premium');
    });

    test('drops a package with no id, which the API would reject', () {
      final packages = MyPackages.fromProfile({
        'packages': {
          'starter': {'package_type': 'starter', 'price': '1', 'delivery_days': 1},
          'pro': {'id': 'b', 'package_type': 'pro', 'price': '10', 'delivery_days': 1},
        },
      });

      expect(packages.all.length, 1);
      expect(packages.all.single.id, 'b');
    });

    test('an empty packages object reports no packages', () {
      final packages = MyPackages.fromProfile({
        'packages': {'starter': null, 'pro': null},
      });
      expect(packages.isEmpty, isTrue);
    });

    test('a missing packages key reports no packages instead of throwing', () {
      expect(MyPackages.fromProfile({'full_name': 'Ada'}).isEmpty, isTrue);
      expect(MyPackages.fromProfile(<String, dynamic>{}).isEmpty, isTrue);
    });

    test('splits an unsplit description into bullets', () {
      final packages = MyPackages.fromProfile({
        'packages': {
          'starter': {
            'id': 'a',
            'price': '1',
            'delivery_days': 1,
            'description': 'Logo design\nTwo revisions\n',
          },
        },
      });

      expect(packages.all.single.description, ['Logo design', 'Two revisions']);
    });
  });

  group('SubmittedProposal', () {
    test('reads the stored bid_amount and delivery_days', () {
      final created = SubmittedProposal.tryFrom({
        'id': 17,
        'project_id': 4,
        'project_title': 'Task Management Web Application',
        'freelancer_id': '2f1c9e40-0000-4000-8000-000000000003',
        'package_id': '2f1c9e40-0000-4000-8000-000000000001',
        'cover_letter': 'I have built eight of these.',
        'bid_amount': '250.00',
        'delivery_days': 7,
        'status': 'pending',
      })!;

      expect(created.id, 17);
      expect(created.projectId, 4);
      expect(created.status, 'pending');
      // The service copies these off the package, so they echo the tier, not
      // anything the form chose.
      expect(created.bidAmount, '250');
      expect(created.deliveryDays, 7);
    });

    test('returns null for a non-object body, such as an error envelope', () {
      expect(SubmittedProposal.tryFrom(null), isNull);
      expect(SubmittedProposal.tryFrom('nope'), isNull);
      expect(SubmittedProposal.tryFrom(<int>[]), isNull);
    });
  });
}
