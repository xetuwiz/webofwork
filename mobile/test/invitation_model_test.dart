import 'package:flutter_test/flutter_test.dart';
import 'package:webofwork/services/invitation_service.dart';

void main() {
  group('InvitationStatus.normalize', () {
    // `JsonStringEnumConverter(SnakeCaseLower)` at `Program.cs:79` means these
    // arrive as lower snake_case strings, not ints.
    test('passes the four wire values through unchanged', () {
      expect(InvitationStatus.normalize('pending'), 'pending');
      expect(InvitationStatus.normalize('accepted'), 'accepted');
      expect(InvitationStatus.normalize('declined'), 'declined');
      expect(InvitationStatus.normalize('cancelled'), 'cancelled');
    });

    test('folds casing and stray whitespace onto the wire value', () {
      expect(InvitationStatus.normalize('  ACCEPTED  '), 'accepted');
      expect(InvitationStatus.normalize('Pending'), 'pending');
      expect(InvitationStatus.normalize('Cancelled'), 'cancelled');
    });

    // `ProposalEnums.InvitationStatus` is Pending=0, Accepted=1, Declined=2,
    // Cancelled=3, so an int fallback is still mapped if one ever appears.
    test('maps the int spellings onto the wire values', () {
      expect(InvitationStatus.normalize('0'), 'pending');
      expect(InvitationStatus.normalize('1'), 'accepted');
      expect(InvitationStatus.normalize('2'), 'declined');
      expect(InvitationStatus.normalize('3'), 'cancelled');
    });

    // The template shape used '0'/'1'/'4' with 4 meaning "hired". WebOfWork's
    // enum stops at Cancelled, so a row can never be hired -- and '4' must not
    // be invented as a state the server does not have.
    test('there is no hired status, and int 4 is not silently promoted', () {
      expect(InvitationStatus.all, isNot(contains('hired')));
      expect(
        InvitationStatus.all,
        ['pending', 'accepted', 'declined', 'cancelled'],
      );
      expect(InvitationStatus.normalize('4'), isNot('accepted'));
      expect(InvitationStatus.normalize('4'), isNot('hired'));
    });

    test('a missing status is treated as pending so the row stays actionable', () {
      expect(InvitationStatus.normalize(null), 'pending');
      expect(InvitationStatus.normalize(''), 'pending');
      expect(InvitationStatus.normalize('   '), 'pending');
    });
  });

  group('Invitation.fromJson', () {
    test('reads the shape InvitationsController projects', () {
      final invitation = Invitation.fromJson({
        'id': 41,
        'client_id': 7,
        'client_name': 'Acme Studio',
        'project_title': 'Landing page',
        'message': 'Come work with us',
        'status': 'pending',
        'created_at': '2026-09-01T10:00:00.000Z',
        'package_type': 'pro',
        // `package_amount` is a `decimal?`; `DecimalAsStringJsonConverter`
        // (Program.cs:80) means it arrives quoted.
        'package_amount': '450.00',
        'contract_info': {'id': 3, 'status': 'offered'},
      });

      expect(invitation.id, 41);
      expect(invitation.clientName, 'Acme Studio');
      expect(invitation.projectTitle, 'Landing page');
      expect(invitation.status, 'pending');
      expect(invitation.isPending, isTrue);
      expect(invitation.isActionable, isTrue);
      expect(invitation.packageType, 'pro');
      expect(invitation.packageAmount, '450.00');
      expect(invitation.contractInfo?['status'], 'offered');
      expect(invitation.createdAt, isNotNull);
    });

    // `isPending` gates the Accept/Decline buttons. If it is always false the
    // freelancer has no way to answer an invitation from the app at all, which is
    // exactly what the old int-comparison code caused.
    test('isPending and isActionable are true only for a pending row', () {
      Invitation withStatus(Object? raw) => Invitation.fromJson({'id': 1, 'status': raw});

      expect(withStatus('pending').isPending, isTrue);
      expect(withStatus('0').isPending, isTrue);
      expect(withStatus('accepted').isPending, isFalse);
      expect(withStatus('1').isPending, isFalse);
      expect(withStatus('declined').isPending, isFalse);
      expect(withStatus('cancelled').isPending, isFalse);
    });

    test('tolerates a row with nothing but an id', () {
      final invitation = Invitation.fromJson({'id': 9});

      expect(invitation.id, 9);
      expect(invitation.clientName, '');
      expect(invitation.projectTitle, '');
      expect(invitation.message, isNull);
      expect(invitation.status, 'pending');
    });

    test('a string id is coerced to an int', () {
      expect(Invitation.fromJson({'id': '41'}).id, 41);
    });
  });

  group('Invitation.copyWith', () {
    test('changes only the status', () {
      final original = Invitation.fromJson({
        'id': 5,
        'client_name': 'Acme',
        'project_title': 'Site',
        'status': 'pending',
      });

      final accepted = original.copyWith(status: 'accepted');

      expect(accepted.status, 'accepted');
      expect(accepted.id, original.id);
      expect(accepted.clientName, original.clientName);
      expect(accepted.projectTitle, original.projectTitle);
    });

    test('a null status keeps the current one, so a rollback cannot blank it', () {
      final original = Invitation.fromJson({'id': 5, 'status': 'declined'});

      expect(original.copyWith().status, 'declined');
    });
  });
}
