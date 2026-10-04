import 'package:flutter_test/flutter_test.dart';
import 'package:webofwork/helper/app_urls.dart';
import 'package:webofwork/models/project_details_model.dart';

/// The exact shape `ProjectsController.Details` returns for
/// `GET /api/projects/{id}/details`.
Map<String, dynamic> projectDetails({
  bool isApplied = false,
  String pricingType = 'fixed',
  dynamic fixedPrice = '1200.00',
  dynamic minBudget = null,
  dynamic maxBudget = null,
  Map<String, dynamic>? clientInfo,
}) =>
    {
      'id': 42,
      'title': 'Landing page redesign',
      'description': 'Rebuild our marketing site.',
      'requirements': 'Must be responsive.',
      'expected_deliverables': 'One Figma file, one deployed build.',
      'category': 5,
      'category_name': 'Web Development',
      'pricing_type': pricingType,
      'fixed_price': fixedPrice,
      'min_budget': minBudget,
      'max_budget': maxBudget,
      'delivery_days': 14,
      'status': 'open',
      'created_at': '2026-09-20T08:00:00.000Z',
      'is_active': true,
      'proposals_count': 3,
      'is_applied': isApplied,
      'skills': <String>['React', 'TypeScript'],
      'client_info': clientInfo ??
          {
            'id': '0f1e2d3c-0000-0000-0000-000000000000',
            'full_name': 'Ada Lovelace',
            'profile_photo': 'https://cdn.example/ada.png',
            'company_name': 'Analytical Engines Ltd',
            // decimal -> quoted, via DecimalAsStringJsonConverter
            'average_rating': '4.80',
            'review_count': 12,
            'total_jobs_posted': 7,
          },
    };

void main() {
  group('ProjectDetailsModel envelope', () {
    // The template read a nested `job_details` key, which this API does not
    // send. `projectDetails` was therefore always null and the job-details screen
    // rendered `jobDetailsNotFound` for every project.
    test('parses the bare object the endpoint actually returns', () {
      final model = ProjectDetailsModel.fromJson(projectDetails());

      expect(model.projectDetails, isNotNull);
      expect(model.projectDetails!.id, 42);
      expect(model.projectDetails!.title, 'Landing page redesign');
    });

    test('still tolerates a legacy job_details wrapper', () {
      // Defensive: if anything ever re-wraps the payload, the screen keeps
      // working instead of blanking.
      final model = ProjectDetailsModel.fromJson({
        'job_details': projectDetails(),
      });

      expect(model.projectDetails, isNotNull);
      expect(model.projectDetails!.id, 42);
    });

    test('an empty body reports nothing loaded rather than throwing', () {
      expect(ProjectDetailsModel.fromJson(<String, dynamic>{}).projectDetails, isNull);
    });
  });

  group('is_applied', () {
    // THE regression. The old line was
    //   json["check_proposal_send_or_not"].toString() != "0"
    // Dart renders a missing key as the string "null", which is not "0", so the
    // comparison was ALWAYS true and the Apply button was permanently disabled
    // with the label "Already proposed" -- on projects never bid on.
    test('is false when the server says the freelancer has not applied', () {
      final model = ProjectDetailsModel.fromJson(projectDetails());
      expect(model.projectDetails!.isApplied, isFalse);
    });

    test('is true only when is_applied is actually true', () {
      expect(
        ProjectDetailsModel.fromJson(projectDetails(isApplied: true)).projectDetails!.isApplied,
        isTrue,
      );
    });

    test('a payload with no is_applied at all means NOT applied', () {
      // Guards the exact trap: absent must never read as applied.
      final json = projectDetails()..remove('is_applied');
      expect(ProjectDetailsModel.fromJson(json).projectDetails!.isApplied, isFalse);
    });

    test('a string "false" is not applied either', () {
      final json = projectDetails()..['is_applied'] = 'false';
      expect(ProjectDetailsModel.fromJson(json).projectDetails!.isApplied, isFalse);
    });
  });

  group('budget', () {
    // `Program.cs` registers DecimalAsStringJsonConverter, so prices arrive
    // quoted. Assigning one straight to a `num` is a runtime TypeError.
    test('reads a quoted fixed price', () {
      final model = ProjectDetailsModel.fromJson(projectDetails());
      expect(model.projectDetails!.fixedPrice, 1200.00);
      expect(model.projectDetails!.isFixedPrice, isTrue);
      expect(model.projectDetails!.displayPrice, 1200.00);
    });

    test('a range project shows the midpoint and the full range', () {
      final model = ProjectDetailsModel.fromJson(projectDetails(
        pricingType: 'range',
        fixedPrice: null,
        minBudget: '500.00',
        maxBudget: '1500.00',
      ));
      final job = model.projectDetails!;

      expect(job.isFixedPrice, isFalse);
      expect(job.displayPrice, 1000.00);
      expect(job.budgetLabel, '500.00 - 1500.00');
    });

    test('a range with only one bound still yields a number', () {
      final model = ProjectDetailsModel.fromJson(projectDetails(
        pricingType: 'range',
        fixedPrice: null,
        minBudget: '500.00',
      ));

      expect(model.projectDetails!.displayPrice, 500.00);
    });

    test('no pricing at all reports a dash rather than 0.00', () {
      final model = ProjectDetailsModel.fromJson(
        projectDetails(fixedPrice: null),
      );
      expect(model.projectDetails!.displayPrice, isNull);
      expect(model.projectDetails!.budgetLabel, '-');
    });
  });

  group('other fields', () {
    test('delivery_days is an int, not a pre-formatted string', () {
      final model = ProjectDetailsModel.fromJson(projectDetails());
      expect(model.projectDetails!.deliveryDays, 14);
    });

    // The server sends a List<string> of names. The old model expected a list of
    // objects, which rendered as [object Object] per entry.
    test('skills are plain names', () {
      final model = ProjectDetailsModel.fromJson(projectDetails());
      expect(model.projectDetails!.skills, ['React', 'TypeScript']);
    });

    test('proposals_count comes from the real counter', () {
      // The old model read a nested `job_proposals` array that does not exist,
      // so the count on every job card was always zero.
      final model = ProjectDetailsModel.fromJson(projectDetails());
      expect(model.projectDetails!.proposalsCount, 3);
    });

    test('status is the snake_case string', () {
      final model = ProjectDetailsModel.fromJson(projectDetails());
      expect(model.projectDetails!.status, 'open');
      expect(model.projectDetails!.isOpen, isTrue);
    });

    test('the brief sections are kept', () {
      final model = ProjectDetailsModel.fromJson(projectDetails());
      expect(model.projectDetails!.requirements, 'Must be responsive.');
      expect(
          model.projectDetails!.expectedDeliverables, 'One Figma file, one deployed build.');
    });

    test('a null client_info leaves the client null rather than throwing', () {
      // `GET /api/projects/{id}` never populates client_info, so this is the
      // shape a mis-pointed URL produces.
      final json = projectDetails()..['client_info'] = null;
      expect(ProjectDetailsModel.fromJson(json).projectDetails!.client, isNull);
    });

    test('a missing client_info key behaves the same as an explicit null', () {
      final json = projectDetails()..remove('client_info');
      expect(ProjectDetailsModel.fromJson(json).projectDetails!.client, isNull);
    });
  });

  group('client_info', () {
    test('reads the nested object, including the quoted rating', () {
      final model = ProjectDetailsModel.fromJson(projectDetails());
      final client = model.projectDetails!.client!;

      expect(client.fullName, 'Ada Lovelace');
      expect(client.companyName, 'Analytical Engines Ltd');
      expect(client.averageRating, 4.80);
      expect(client.reviewCount, 12);
      expect(client.totalJobsPosted, 7);
    });

    // The old "About the client" card showed "Member since <today>" for every
    // project because it read a `created_at` that is not on this payload. The
    // card no longer has a join-date row at all, so there is nothing to fake --
    // this asserts the model offers no such value.
    test('offers no join date, because the payload has none', () {
      final model = ProjectDetailsModel.fromJson(projectDetails());
      expect(model.projectDetails!.client!.toJson().containsKey('created_at'), isFalse);
      expect(model.projectDetails!.client!.toJson().containsKey('hiring_rate'), isFalse);
    });

    test('an all-null client_info still parses instead of throwing', () {
      final model = ProjectDetailsModel.fromJson(projectDetails(
        clientInfo: <String, dynamic>{
          'id': null,
          'full_name': null,
          'average_rating': null,
          'review_count': null,
        },
      ));
      final client = model.projectDetails!.client!;

      expect(client.fullName, '');
      expect(client.averageRating, 0);
      expect(client.reviewCount, 0);
      expect(client.initial, '');
    });

    test('the avatar placeholder uses the first letter of the name', () {
      final model = ProjectDetailsModel.fromJson(projectDetails());
      expect(model.projectDetails!.client!.initial, 'A');
    });
  });

  group('the URL the screen requests', () {
    // `ProjectsController.Get` (`GET /api/projects/{id}`) is [AllowAnonymous]
    // and returns MapToResponse(...), which leaves `client_info` null and sends
    // no `is_applied` at all. `ProjectsController.Details`
    // (`GET /api/projects/{id}/details`) is [Authorize(RequireFreelancer)] and
    // populates both. Reading the wrong one is how the client rendered as blank.
    test('targets the freelancer-shaped /details route', () {
      final url = AppUrls.projectDetailsForFreelancerUrl(42);

      expect(url, endsWith('/api/projects/42/details'));
    });

    test('is distinct from the anonymous project route', () {
      expect(
        AppUrls.projectDetailsForFreelancerUrl(42),
        isNot(endsWith('/api/projects/42')),
      );
    });
  });
}
