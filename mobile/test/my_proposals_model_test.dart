import 'package:flutter_test/flutter_test.dart';
import 'package:webofwork/helper/app_urls.dart';
import 'package:webofwork/models/contract_model.dart';
import 'package:webofwork/models/my_proposals_model.dart';

void main() {
  group('MyProposalsPage envelope', () {
    // `ProposalsController.GetMyProposals` answers
    // `{count, next, previous, results}`. The template read `json["my_proposals"]`
    // and `json["data"]`, neither of which the server sends, so
    // `myProposals?.data` was always null and the My Proposals list rendered
    // empty on a 200.
    test('reads the envelope the endpoint actually returns', () {
      final page = MyProposalsPage.fromJson({
        'count': 24,
        'next': '?page=2&pageSize=10',
        'previous': null,
        'results': [
          {
            'id': 7,
            'project_id': 3,
            'project_title': 'Landing page',
            'status': 'pending',
            'bid_amount': '450.00',
            'delivery_days': 7,
            'cover_letter': 'I have done this before.',
            'created_at': '2026-09-20T08:00:00.000Z',
          },
        ],
      });

      expect(page.count, 24);
      expect(page.next, '?page=2&pageSize=10');
      expect(page.previous, isNull);
      expect(page.hasNext, isTrue);
      expect(page.results, hasLength(1));
    });

    test('an absent next means there is nothing more to page', () {
      final page = MyProposalsPage.fromJson({
        'count': 1,
        'next': null,
        'results': [
          {'id': 1},
        ],
      });

      expect(page.hasNext, isFalse);
    });

    test('tolerates a bare array, which is what the endpoint used to send', () {
      final page = MyProposalsPage.fromJson([
        {'id': 1},
        {'id': 2},
      ]);

      expect(page.results, hasLength(2));
      expect(page.count, 2);
      expect(page.hasNext, isFalse);
    });

    test('an empty body reports an empty page rather than throwing', () {
      expect(MyProposalsPage.fromJson(null).results, isEmpty);
      expect(MyProposalsPage.fromJson('nonsense').results, isEmpty);
      expect(MyProposalsPage.fromJson(<String, dynamic>{}).results, isEmpty);
    });
  });

  group('MyProposal fields', () {
    // `CreateProposalRequest` has no attachment, and `ProposalResponse` has no
    // path column, so the old card built "${path}/${attachment}" from two values
    // that were always empty.
    test('reads the ProposalResponse shape', () {
      final p = MyProposal.fromJson(const {
        'id': 12,
        'project_id': 4,
        'project_title': 'React dashboard',
        'freelancer_id': '0f1e...',
        'freelancer_name': 'Ada L',
        'package_id': '9a8b...',
        'cover_letter': 'Here is my approach.',
        // `DecimalAsStringJsonConverter` (Program.cs:80) -- quoted.
        'bid_amount': '1200.50',
        'delivery_days': 14,
        'status': 'accepted',
        'created_at': '2026-09-20T08:00:00.000Z',
        'freelancer_photo': 'https://cdn/x.png',
        'freelancer_tagline': 'Full-stack dev',
        'freelancer_ratings': '4.80',
        'contract_info': {'id': 3, 'status': 'active', 'is_this_freelancer': true},
      });

      expect(p.id, 12);
      expect(p.projectId, 4);
      expect(p.projectTitle, 'React dashboard');
      expect(p.bidAmount, 1200.50);
      expect(p.deliveryDays, 14);
      expect(p.coverLetter, 'Here is my approach.');
      expect(p.freelancerRatings, 4.80);
      expect(p.contractInfo?.id, 3);
      expect(p.contractInfo?.isThisFreelancer, isTrue);
      expect(p.createdAt, isNotNull);
    });

    test('the title is flattened onto the row, not nested under job', () {
      // The old model read `job?.title`, which is always null, so every card
      // showed no project title at all.
      final p = MyProposal.fromJson(const {
        'id': 1,
        'project_title': 'Brand refresh',
      });

      expect(p.projectTitle, 'Brand refresh');
    });

    test('a missing contract_info leaves the proposal unactioned', () {
      final p = MyProposal.fromJson(const {'id': 1, 'status': 'pending'});

      expect(p.contractInfo, isNull);
      expect(p.isActioned, isFalse);
    });
  });

  group('MyProposal status', () {
    // `ProposalStatus` is Pending=0, Accepted=1, Rejected=2, Withdrawn=3 and is
    // emitted by `JsonStringEnumConverter(SnakeCaseLower)`. The template's
    // `isHired` / `isShortListed` / `isInterviewTake` / `isView` / `isRejected`
    // flags were template columns this enum does not have, so they were always
    // false and the status chips never appeared.
    test('maps the four wire values', () {
      MyProposal withStatus(String s) => MyProposal.fromJson({'id': 1, 'status': s});

      expect(withStatus('pending').statusLabel, 'Pending');
      expect(withStatus('accepted').statusLabel, 'Accepted');
      expect(withStatus('rejected').statusLabel, 'Rejected');
      expect(withStatus('withdrawn').statusLabel, 'Withdrawn');
    });

    test('an unrecognised status is shown verbatim rather than hidden', () {
      expect(
        MyProposal.fromJson(const {'id': 1, 'status': 'shortlisted'}).statusLabel,
        'shortlisted',
      );
    });

    // `ProposalService.WithdrawProposalAsync` throws unless `Pending`, so the
    // button is hidden on anything else rather than shown and then rejected.
    test('canWithdraw only for a pending proposal', () {
      MyProposal withStatus(String s) => MyProposal.fromJson({'id': 1, 'status': s});

      expect(withStatus('pending').canWithdraw, isTrue);
      expect(withStatus('accepted').canWithdraw, isFalse);
      expect(withStatus('rejected').canWithdraw, isFalse);
      expect(withStatus('withdrawn').canWithdraw, isFalse);
      expect(withStatus('').canWithdraw, isFalse);
    });
  });

  group('MyProposalsUrl', () {
    // `ProposalsController` is `[Route("api/[controller]")]` and its only GETs
    // are `my` and `project/{projectId}` -- there is no handler on the bare
    // prefix, so the old value 404'd.
    test('points at a route that exists', () {
      expect(AppUrls.myProposalsUrl, endsWith('/api/projects/my-proposals'));
    });

    test('builds a page-1 request with the pageSize the server expects', () {
      final url = AppUrls.myProposalsUrlWithQuery();
      final query = Uri.splitQueryString(Uri.parse(url).query);

      expect(query['page'], '1');
      expect(query['pageSize'], '10');
      expect(query.containsKey('status'), isFalse);
    });

    test('passes a status filter through', () {
      final url = AppUrls.myProposalsUrlWithQuery(status: 'accepted');
      final query = Uri.splitQueryString(Uri.parse(url).query);

      expect(query['status'], 'accepted');
    });

    // The server returns `next` as a relative `?page=2&pageSize=10`, which is not
    // a URL. Handing it straight to the network layer was the other half of the
    // broken pagination.
    test('joins a relative next query string onto the endpoint', () {
      final url = AppUrls.myProposalsUrlWithQuery(next: '?page=2&pageSize=10');

      expect(url, contains('/api/projects/my-proposals'));
      expect(url, endsWith('?page=2&pageSize=10'));
    });

    test('leaves an absolute next URL alone', () {
      const absolute = 'https://api.example.com/api/projects/my-proposals?page=2';
      expect(AppUrls.myProposalsUrlWithQuery(next: absolute), absolute);
    });
  });

  group('ContractPage still covers the offers endpoint', () {
    // `AppUrls.myOffersUrl` is `GET /api/contracts?status=offered`, which answers
    // `{count, next, previous, results}`. The old offers model read
    // `json["my_offers"]` and so was permanently empty.
    test('reads the contracts envelope', () {
      final page = ContractPage.fromJson({
        'count': 2,
        'next': null,
        'previous': null,
        'results': [
          {'id': 1, 'status': 'offered', 'total_amount': '100.00'},
          {'id': 2, 'status': 'offered', 'total_amount': '200.00'},
        ],
      });

      expect(page.count, 2);
      expect(page.results, hasLength(2));
      expect(page.results.every((c) => c.status == Contract.statusOffered), isTrue);
    });

    test('still tolerates the bare array my_offers returns', () {
      final page = ContractPage.fromJson([
        {'id': 1, 'status': 'offered'},
      ]);

      expect(page.count, 1);
      expect(page.results.single.status, Contract.statusOffered);
    });
  });
}
