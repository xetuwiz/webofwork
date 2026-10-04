import 'package:flutter_test/flutter_test.dart';
import 'package:webofwork/services/invitation_service.dart';

/// The match-context fields on an invitation.
///
/// These matter because they are the first thing a freelancer sees that is
/// different about an AI-matched invitation, and because a silent parse failure
/// here is invisible: the card just does not appear, and nothing says why.
void main() {
  group('Invitation.matchContext', () {
    Map<String, dynamic> invitationJson([Map<String, dynamic>? matchContext]) => {
          'id': 7,
          'client_name': 'Acme',
          'project_title': 'Customer portal',
          'status': 'pending',
          if (matchContext != null) 'match_context': matchContext,
        };

    test('is null when the server sent no reasoning', () {
      // A hand-picked invitation has none, and inventing a placeholder would
      // present a client's choice as a platform judgement.
      expect(Invitation.fromJson(invitationJson()).matchContext, isNull);
    });

    test('is null for an empty object rather than a context with nothing in it', () {
      // "There is no reasoning" and "the reasoning is empty" are different
      // states; only the first should suppress the card.
      expect(Invitation.fromJson(invitationJson({})).matchContext, isNull);
    });

    test('parses the score, reason, strengths and gaps', () {
      final invitation = Invitation.fromJson(invitationJson({
        'score': 87.5,
        'reason': 'Strong React history with comparable billing work.',
        'strengths': ['React', 'Stripe'],
        'gaps': ['C#'],
      }));

      final match = invitation.matchContext!;
      expect(match.score, 87.5);
      expect(match.reason, contains('React history'));
      expect(match.strengths, ['React', 'Stripe']);
      expect(match.gaps, ['C#']);
    });

    test('survives missing lists', () {
      final match = Invitation.fromJson(
        invitationJson({'score': 40, 'reason': 'Partial overlap.'}),
      ).matchContext!;

      expect(match.strengths, isEmpty);
      expect(match.gaps, isEmpty);
    });

    test('tolerates a list of the wrong shape', () {
      // The server is trusted to send lists, but a null must not take the whole
      // invitations list down with it.
      //
      // A bare string is read as newline-delimited rather than rejected, which
      // is the shared `asStringList` convention this codebase uses for
      // textarea input. Asserted here so the behaviour is a decision on record
      // rather than a surprise if the helper ever changes.
      final match = Invitation.fromJson(
        invitationJson({
          'score': 10,
          'strengths': null,
          'gaps': 'C#\nAzure',
        }),
      ).matchContext!;

      expect(match.strengths, isEmpty);
      expect(match.gaps, ['C#', 'Azure']);
    });

    test('is immutable, so a card cannot mutate the list it was handed', () {
      final match = MatchContext(score: 90, strengths: ['React']);
      expect(() => match.strengths.add('C#'), throwsUnsupportedError);
    });
  });

  group('MatchContext.hasContent', () {
    test('is false for a match with no explanation at all', () {
      // This is the gate on the card. A panel that renders with nothing in it
      // implies the platform checked and had nothing to say, which is a
      // different claim from having no reasoning.
      final match = MatchContext(score: 55);
      expect(match.hasContent, isFalse);
    });

    test('is false when the reason is only whitespace', () {
      expect(MatchContext(score: 55, reason: '   ').hasContent, isFalse);
    });

    test('is true for a reason alone', () {
      expect(MatchContext(score: 55, reason: 'Closest rating.').hasContent, isTrue);
    });

    test('is true for lists alone', () {
      expect(MatchContext(score: 55, gaps: ['C#']).hasContent, isTrue);
    });
  });

  group('MatchContext.scoreAsPercent', () {
    // The server sends 0-100. Rendering it as a proportion shows "0.9%" for a
    // strong match, which reads as a bug to the user and is invisible in tests
    // that only check for a number.
    test('renders the server scale unchanged', () {
      expect(MatchContext(score: 87.5).scoreAsPercent, '88%');
      expect(MatchContext(score: 0).scoreAsPercent, '0%');
      expect(MatchContext(score: 100).scoreAsPercent, '100%');
    });

    test('scales a value that arrived as a proportion', () {
      expect(MatchContext(score: 0.875).scoreAsPercent, '88%');
      expect(MatchContext(score: 1).scoreAsPercent, '100%');
    });
  });
}
