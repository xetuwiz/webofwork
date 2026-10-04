import 'package:flutter_test/flutter_test.dart';
import 'package:webofwork/models/contract_model.dart';

/// These pin the wire contract of `ContractResponse`.
///
/// This domain was silently blank for the whole port: the models read a
/// template shape (`price`, `job`, `user.first_name`, `commission_amount`,
/// `is_project_job`, `next_page_url`, integer `status`) while the server emits a
/// flat snake_case `ContractResponse` with quoted decimals and snake_case enum
/// strings. Every field parsed as `null`, so the orders list rendered blank rows
/// and the dashboard's active-contracts filter -- an `int` comparison against a
/// string -- never matched a single contract.
///
/// Each test names the exact thing that was wrong, so a regression names itself.
void main() {
  // A body shaped exactly like the server's: snake_case keys, `total_amount` and
  // `freelancer_earnings` as **quoted** decimals (DecimalAsStringJsonConverter),
  // and `status` as a snake_case enum string.
  Map<String, dynamic> body([Map<String, dynamic> overrides = const {}]) => {
        'id': 42,
        'project_id': 7,
        'project_title': 'Redesign the marketing site',
        'project_description': 'A full redesign.',
        'client_id': 'c1c1c1c1-0000-0000-0000-000000000001',
        'client_name': 'Acme Ltd',
        'freelancer_id': 'f2f2f2f2-0000-0000-0000-000000000002',
        'freelancer_name': 'Xetuwiz',
        'freelancer_user_id': 'u2u2u2u2-0000-0000-0000-000000000003',
        'client_user_id': 'u1u1u1u1-0000-0000-0000-000000000004',
        'package_id': '9',
        'package_name': 'Pro',
        'delivery_days': 14,
        'total_amount': '1250.00',
        'freelancer_earnings': '1125.00',
        'is_funded': true,
        'paid_to_freelancer': false,
        'payout_eligible_after': '2026-10-01T00:00:00.000Z',
        'client_has_signed': true,
        'client_signed_at': '2026-09-01T10:00:00.000Z',
        'freelancer_has_signed': false,
        'freelancer_signed_at': null,
        'legal_document_url': 'https://cdn.example/contract_42.pdf',
        'completed_at': null,
        'status': 'active',
        'payment_status': 'pending',
        'requirements': 'Must be responsive.',
        'expected_deliverables': 'Figma file and exported assets',
        'skills': ['Figma', 'Design systems'],
        'deliverables': <dynamic>[],
        'revisions': <dynamic>[],
        ...overrides,
      };

  group('Contract.fromJson reads the real ContractResponse', () {
    test('scalars come from snake_case keys, not the template\'s names', () {
      final c = Contract.fromJson(body());

      expect(c.id, 42);
      expect(c.projectId, 7);
      expect(c.projectTitle, 'Redesign the marketing site');
      expect(c.clientName, 'Acme Ltd');
      expect(c.freelancerName, 'Xetuwiz');
      expect(c.packageName, 'Pro');
      expect(c.deliveryDays, 14);
      expect(c.paymentStatus, 'pending');
    });

    test('total_amount and freelancer_earnings parse as quoted decimals', () {
      // DecimalAsStringJsonConverter writes decimals as JSON strings; reading
      // them into a `num` was the runtime TypeError that crashed the old apply
      // sheet on tier tap.
      final c = Contract.fromJson(body());

      expect(c.totalAmount, 1250.00);
      expect(c.freelancerEarnings, 1125.00);
    });

    test('status is the snake_case string, and isLive matches it as a string', () {
      // The dashboard filtered on `status == 1 || status == 2` against these
      // strings, so the list was always empty.
      final active = Contract.fromJson(body());
      expect(active.status, 'active');
      expect(active.isLive, isTrue);

      // An int comparison against the wire string can never be true, which is
      // the bug; pin the string form so a return to ordinals is caught.
      expect(active.status == 1, isFalse);

      expect(Contract.fromJson(body({'status': 'completed'})).isLive, isFalse);
      expect(Contract.fromJson(body({'status': 'completed'})).isClosed, isTrue);
      expect(Contract.fromJson(body({'status': 'cancelled'})).isClosed, isTrue);
      expect(Contract.fromJson(body({'status': 'submitted'})).isLive, isTrue);
    });

    test('an unknown status still yields a readable label instead of throwing', () {
      final c = Contract.fromJson(body({'status': 'under_review'}));

      expect(c.statusLabel, 'under_review');
      expect(c.isLive, isFalse);
      expect(c.isClosed, isFalse);
    });

    test('an empty status is labelled rather than rendered blank', () {
      final c = Contract.fromJson(body({'status': null}));
      expect(c.statusLabel, 'Unknown');
    });

    test('the signature and funding flags drive the available actions', () {
      // needsMySignature is what decides whether "Accept contract" appears, and
      // acceptsWork whether "Submit work" does.
      final awaitingSignature = Contract.fromJson(body({'freelancer_has_signed': false}));
      expect(awaitingSignature.needsMySignature, isTrue);
      expect(awaitingSignature.acceptsWork, isFalse);

      final signed = Contract.fromJson(body({
        'freelancer_has_signed': true,
        'freelancer_signed_at': '2026-09-02T10:00:00.000Z',
      }));
      expect(signed.needsMySignature, isFalse);
      expect(signed.isSignedByBoth, isTrue);
      expect(signed.acceptsWork, isTrue);
    });

    test('a cancelled contract needs no signature and accepts no work', () {
      final c = Contract.fromJson(body({
        'status': 'cancelled',
        'freelancer_has_signed': false,
      }));
      expect(c.needsMySignature, isFalse);
      expect(c.acceptsWork, isFalse);
    });

    test('freelancer_user_id is read, because chat matches on users.id', () {
      // `freelancer_id` is the freelancer_profiles row, not users.id. Chat room
      // creation does an EXISTS check against users.id, so passing the profile
      // id 403s.
      final c = Contract.fromJson(body());
      expect(c.freelancerUserId, 'u2u2u2u2-0000-0000-0000-000000000003');
      expect(c.clientUserId, 'u1u1u1u1-0000-0000-0000-000000000004');
    });

    test('a null package_id becomes null, not an empty string', () {
      // A nullable uuid arriving as "" reads as "present but empty" at every
      // call site, which is how a custom-offer contract looked package-backed.
      expect(Contract.fromJson(body({'package_id': null})).packageId, isNull);
      expect(Contract.fromJson(body({'package_id': '  '})).packageId, isNull);
    });

    test('an unsplit newline blob of skills becomes a list', () {
      final c = Contract.fromJson(body({'skills': 'Figma\nDesign systems\n\nWebflow'}));
      expect(c.skills, ['Figma', 'Design systems', 'Webflow']);
    });

    test('skills fall back to required_technologies, which the server aliases', () {
      final withoutSkills = body()..remove('skills');
      expect(Contract.fromJson(withoutSkills).skills, isEmpty);

      final aliased = body()..remove('skills');
      aliased['required_technologies'] = ['Blender'];
      expect(Contract.fromJson(aliased).skills, ['Blender']);
    });

    test('a malformed deliverable row cannot empty the whole list', () {
      final c = Contract.fromJson(body({
        'deliverables': [
          'not an object',
          {
            'id': 5,
            'contract_id': 42,
            'title': 'Homepage design',
            'file_url': 'https://cdn.example/home.zip',
            'notes': 'v1',
            'deliverable_type': 'file',
            'submitted_at': '2026-09-10T08:00:00.000Z',
          },
          null,
        ],
      }));

      expect(c.deliverables, hasLength(1));
      expect(c.deliverables.first.title, 'Homepage design');
      expect(c.deliverables.first.attachment, 'https://cdn.example/home.zip');
      expect(c.deliverables.first.isLink, isFalse);
    });

    test('a link deliverable is recognised and preferred over nothing', () {
      final c = Contract.fromJson(body({
        'deliverables': [
          {
            'id': 6,
            'contract_id': 42,
            'title': 'Staging',
            'link_url': 'https://staging.example',
            'deliverable_type': 'link',
            'submitted_at': null,
          },
        ],
      }));

      expect(c.deliverables.first.isLink, isTrue);
      expect(c.deliverables.first.attachment, 'https://staging.example');
      expect(c.deliverables.first.submittedAt, isNull);
    });

    test('a revision resolves its status string and keeps the rejection reason', () {
      final c = Contract.fromJson(body({
        'revisions': [
          {
            'id': 3,
            'contract_id': 42,
            'user_id': 'u1',
            'reason': 'Logo is not the new one',
            'status': 'rejected',
            'rejection_message': 'Sent the 2026 brand pack, please check again',
            'requested_at': '2026-09-11T00:00:00.000Z',
            'resolved_at': '2026-09-12T00:00:00.000Z',
          },
        ],
      }));

      final r = c.revisions.single;
      expect(r.isPending, isFalse);
      expect(r.status, ContractRevision.rejected);
      expect(r.rejectionMessage, 'Sent the 2026 brand pack, please check again');
    });

    test('a pending revision is actionable, which is the freelancer-only route', () {
      final c = Contract.fromJson(body({
        'revisions': [
          {
            'id': 4,
            'contract_id': 42,
            'user_id': 'u1',
            'reason': 'Please swap the font',
            'status': 'pending',
            'rejection_message': null,
            'requested_at': '2026-09-11T00:00:00.000Z',
            'resolved_at': null,
          },
        ],
      }));
      expect(c.revisions.single.isPending, isTrue);
    });

    test('an empty body yields defaults rather than throwing', () {
      // A failed request returns an error envelope, and `CustomFutureWidget`
      // hands the body to the parser regardless.
      final c = Contract.fromJson(const {});

      expect(c.id, 0);
      expect(c.totalAmount, 0);
      expect(c.statusLabel, 'Unknown');
      expect(c.deliverables, isEmpty);
      expect(c.revisions, isEmpty);
    });

    test('booleans sent as "true"/1 are read as true, not silently false', () {
      final c = Contract.fromJson(body({
        'is_funded': 'true',
        'client_has_signed': 1,
        'paid_to_freelancer': '1',
      }));
      expect(c.isFunded, isTrue);
      expect(c.clientHasSigned, isTrue);
      expect(c.paidToFreelancer, isTrue);
    });

    test('copyWith folds an action in without dropping the other fields', () {
      final c = Contract.fromJson(body());
      final signed = c.copyWith(
        status: Contract.statusActive,
        freelancerHasSigned: true,
        freelancerSignedAt: DateTime.utc(2026, 9, 2),
      );

      expect(signed.freelancerHasSigned, isTrue);
      expect(signed.id, c.id);
      expect(signed.totalAmount, c.totalAmount);
      expect(signed.clientHasSigned, c.clientHasSigned);
    });
  });

  group('reviews and dispute_details embed (Phase 3 workspace depth)', () {
    test('reviews maps client_review/freelancer_review without crashing on nulls', () {
      // The server nests the reviews as an object with two nullable halves and
      // the bare-string/bare-map shapes both arrive on the wire; a bare model
      // never parses either half.
      final c = Contract.fromJson(body({
        'reviews': {
          'client_review': {
            'id': 11,
            'contract_id': 42,
            'reviewer_id': 'c1c1c1c1-0000-0000-0000-000000000001',
            'type': 'freelancer',
            'rating': 5,
            'comment': 'Excellent work and fast communication.',
            'reviewer_name': 'Acme Ltd',
            'created_at': '2026-09-20T09:00:00.000Z',
          },
          'freelancer_review': null,
        },
      }));

      expect(c.clientReview, isNotNull);
      expect(c.clientReview!.rating, 5);
      expect(c.clientReview!.type, 'freelancer');
      expect(c.clientReview!.comment, 'Excellent work and fast communication.');
      expect(c.clientReview!.reviewerName, 'Acme Ltd');
      expect(c.freelancerReview, isNull);
    });

    test('both halves parse when the freelancer has reviewed too', () {
      final c = Contract.fromJson(body({
        'reviews': {
          'client_review': {
            'id': 11,
            'type': 'freelancer',
            'rating': 5,
            'comment': 'Great.',
            'created_at': null,
          },
          'freelancer_review': {
            'id': 12,
            'type': 'client',
            'rating': 4,
            'comment': 'Clear brief, quick to respond.',
            'created_at': '2026-09-21T09:00:00.000Z',
          },
        },
      }));

      expect(c.clientReview!.rating, 5);
      expect(c.freelancerReview!.rating, 4);
      expect(c.freelancerReview!.type, 'client');
      expect(c.freelancerReview!.createdAt, isNotNull);
    });

    test('dispute_details parses with the reference reason wire label intact', () {
      // DisputeReasonJsonConverter accepts snake_case and the literal
      // "Unlimited Revisions"; the model must preserve whichever label the
      // server echoed so the banner can display it.
      final c = Contract.fromJson(body({
        'status': 'disputed',
        'dispute_details': {
          'id': 3,
          'contract_id': 42,
          'raised_by_id': 'u2u2u2u2-0000-0000-0000-000000000003',
          'opener_role': 'Freelancer',
          'reason': 'Unlimited Revisions',
          'description': 'The client kept adding rounds of unrequested changes.',
          'status': 'under_review',
          'admin_notes': 'Reviewing the exchange history.',
          'evidence_urls': ['https://cdn.example/chat-log.png'],
          'created_at': '2026-09-18T08:00:00.000Z',
          'resolved_at': null,
        },
      }));

      final d = c.dispute!;
      expect(d.id, 3);
      expect(d.contractId, 42);
      expect(d.raisedById, 'u2u2u2u2-0000-0000-0000-000000000003');
      expect(d.reason, 'Unlimited Revisions');
      expect(d.reasonLabel, 'Unlimited revisions');
      expect(d.evidenceUrls, ['https://cdn.example/chat-log.png']);
      expect(d.isOpen, isTrue);
      // The app is freelancer-only, so an opener_role of Freelancer reads as
      // "raised by me" even though the id is a users.id.
      expect(d.raisedByMe, isTrue);
    });

    test('a managed (resolved) dispute is no longer open and reads labels', () {
      final c = Contract.fromJson(body({
        'status': 'disputed',
        'dispute_details': {
          'id': 4,
          'contract_id': 42,
          'raised_by_id': 'u1u1u1u1-0000-0000-0000-000000000004',
          'opener_role': 'Client',
          'reason': 'poor_quality',
          'description': 'Missed the brief on the hero section.',
          'status': 'resolved',
          'admin_notes': 'Freelancer offered a rework; funds released.',
          'evidence_urls': [],
          'created_at': '2026-09-10T08:00:00.000Z',
          'resolved_at': '2026-09-22T08:00:00.000Z',
        },
      }));

      final d = c.dispute!;
      expect(d.isOpen, isFalse);
      expect(d.statusLabel, 'Resolved');
      expect(d.reasonLabel, 'Poor quality');
      expect(d.raisedByMe, isFalse);
      expect(d.resolvedAt, isNotNull);
    });

    test('a non-disputed contract keeps dispute and both reviews null', () {
      final c = Contract.fromJson(body());
      expect(c.dispute, isNull);
      expect(c.clientReview, isNull);
      expect(c.freelancerReview, isNull);
    });

    test('reviewIn and readDispute tolerate the shapes the server actually sends', () {
      expect(reviewIn(null, 'client_review'), isNull);
      expect(reviewIn('a string', 'client_review'), isNull);
      expect(readDispute(null), isNull);
      expect(readDispute('a string'), isNull);
      expect(readDispute([]), isNull);
    });

    test('canManageDeliverables matches the server EnsureMutable window', () {
      // The PATCH/DELETE gates live on the server too (EnsureMutable), but the
      // UI mirrors them so the controls simply are not drawn off-window.
      Map<String, dynamic> withStatus(String s) => body({'status': s});
      expect(Contract.fromJson(withStatus('active')).canManageDeliverables, isTrue);
      expect(Contract.fromJson(withStatus('submitted')).canManageDeliverables, isTrue);
      expect(Contract.fromJson(withStatus('offered')).canManageDeliverables, isFalse);
      expect(Contract.fromJson(withStatus('completed')).canManageDeliverables, isFalse);
      expect(Contract.fromJson(withStatus('disputed')).canManageDeliverables, isFalse);
      expect(Contract.fromJson(withStatus('cancelled')).canManageDeliverables, isFalse);
      expect(Contract.fromJson(withStatus('refunded')).canManageDeliverables, isFalse);
    });

    test('canSubmitReview mirrors the server Completed|Refunded gate', () {
      Map<String, dynamic> withStatus(String s) => body({'status': s});
      expect(Contract.fromJson(withStatus('completed')).canSubmitReview, isTrue);
      expect(Contract.fromJson(withStatus('refunded')).canSubmitReview, isTrue);
      expect(Contract.fromJson(withStatus('active')).canSubmitReview, isFalse);
      expect(Contract.fromJson(withStatus('disputed')).canSubmitReview, isFalse);
      expect(Contract.fromJson(withStatus('submitted')).canSubmitReview, isFalse);
    });

    test('copyWith folds dispute and reviews without dropping deliverables', () {
      final base = Contract.fromJson(body({
        'deliverables': [
          {
            'id': 5,
            'contract_id': 42,
            'title': 'Homepage design',
            'file_url': 'https://cdn.example/home.zip',
            'deliverable_type': 'file',
            'submitted_at': '2026-09-10T08:00:00.000Z',
          },
        ],
      }));
      final folded = base.copyWith(
        dispute: ContractDispute.fromJson({
          'id': 3,
          'contract_id': 42,
          'raised_by_id': 'u2u2u2u2-0000-0000-0000-000000000003',
          'opener_role': 'Freelancer',
          'reason': 'other',
          'description': 'Details here.',
          'status': 'pending',
        }),
        freelancerReview: ContractReview.fromJson({
          'id': 12,
          'type': 'client',
          'rating': 4,
          'comment': 'Clear brief.',
        }),
      );

      expect(folded.dispute, isNotNull);
      expect(folded.freelancerReview, isNotNull);
      expect(folded.clientReview, isNull);
      // The action must not wipe the list it was folded in beside.
      expect(folded.deliverables, hasLength(1));
      expect(folded.deliverables.first.title, 'Homepage design');
    });
  });

  group('ContractPage follows the {count, next, previous, results} envelope', () {
    test('reads the paging envelope and the count', () {
      // The old OrdersList read `results` correctly but `next_page_url` for the
      // next link and `total_count` for the count, neither of which this API
      // sends -- so paging was permanently dead and the count was always null.
      final page = ContractPage.fromJson({
        'count': 57,
        'next': 'http://host/api/contracts/?page=2',
        'previous': null,
        'results': [body()],
      });

      expect(page.count, 57);
      expect(page.results, hasLength(1));
      expect(page.hasNext, isTrue);
      expect(page.previous, isNull);
    });

    test('a null next means the last page, so no spinner is shown', () {
      final page = ContractPage.fromJson({
        'count': 1,
        'next': null,
        'previous': null,
        'results': [body()],
      });

      expect(page.hasNext, isFalse);
    });

    test('a bare array is accepted, which is what my_offers returns', () {
      final page = ContractPage.fromJson([
        body({'status': 'offered'})
      ]);

      expect(page.results, hasLength(1));
      expect(page.results.single.status, Contract.statusOffered);
      expect(page.hasNext, isFalse);
    });

    test('an error envelope parses to an empty page rather than throwing', () {
      final page = ContractPage.fromJson({'detail': 'Not found.'});

      expect(page.isEmpty, isTrue);
      expect(page.hasNext, isFalse);
    });

    test('a null body parses to an empty page', () {
      expect(ContractPage.fromJson(null).isEmpty, isTrue);
    });
  });

  group('json_coercion', () {
    test('asDouble accepts a quoted decimal, a number and null', () {
      expect(asDouble('1250.00'), 1250.00);
      expect(asDouble(1250), 1250.00);
      expect(asDouble(null), 0);
      expect(asDouble('not a number'), 0);
    });

    test('asInt accepts a quoted int, a number and a double', () {
      expect(asInt('14'), 14);
      expect(asInt(14), 14);
      expect(asInt(14.9), 14);
      expect(asInt(null), 0);
    });

    test('asOptString collapses blank to null', () {
      expect(asOptString('  '), isNull);
      expect(asOptString('x'), 'x');
      expect(asOptString(null), isNull);
    });

    test('asDateOrNull returns null for blank and unparseable input', () {
      expect(asDateOrNull(''), isNull);
      expect(asDateOrNull('nope'), isNull);
      expect(asDateOrNull(null), isNull);
      expect(asDateOrNull('2026-09-01T10:00:00.000Z'), isNotNull);
    });
  });
}
