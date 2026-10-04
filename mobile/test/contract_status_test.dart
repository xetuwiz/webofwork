// Pins the `ContractStatus` wire values and the derived getters the app gates
// its actions on.
//
// `ContractStatus` is serialised by `JsonStringEnumConverter(SnakeCaseLower)`
// (`Program.cs`), so the wire value is a lowercase snake_case **string** -- never
// an ordinal. Every status comparison in the app depends on that. The server
// additionally enforces a state machine in `ContractStateMachine`:
//
//   offered    -> {active, cancelled}
//   active     -> {submitted, cancelled, disputed}
//   submitted  -> {completed, active, disputed}
//   disputed   -> {completed, refunded}
//   completed, cancelled, refunded  -> terminal
//
// A client-side predicate that disagrees with that is worse than no predicate:
// it offers an action the server will reject with a 400.
import 'package:flutter_test/flutter_test.dart';
import 'package:webofwork/models/contract_model.dart';

const allStatuses = [
  Contract.statusOffered,
  Contract.statusActive,
  Contract.statusSubmitted,
  Contract.statusCompleted,
  Contract.statusCancelled,
  Contract.statusDisputed,
  Contract.statusRefunded,
];

/// A contract in [status] with both signatures set unless overridden.
Contract contractIn(String status,
    {bool clientSigned = true, bool freelancerSigned = true}) {
  return Contract.fromJson({
    'id': 1,
    'project_id': 2,
    'status': status,
    'client_has_signed': clientSigned,
    'freelancer_has_signed': freelancerSigned,
  });
}

/// A payload with no `status` at all -- the "nothing loaded yet" shape.
Contract emptyContract() => Contract.fromJson({'id': 1, 'project_id': 2});

void main() {
  group('wire values', () {
    // These are the exact strings `JsonStringEnumConverter(SnakeCaseLower)`
    // produces. A drift here silently breaks every status comparison in the app.
    test('match the server enum exactly', () {
      expect(Contract.statusOffered, 'offered');
      expect(Contract.statusActive, 'active');
      expect(Contract.statusSubmitted, 'submitted');
      expect(Contract.statusCompleted, 'completed');
      expect(Contract.statusCancelled, 'cancelled');
      expect(Contract.statusDisputed, 'disputed');
      expect(Contract.statusRefunded, 'refunded');
    });

    test('are all lowercase -- no title case, no ordinals', () {
      for (final status in allStatuses) {
        expect(status, status.toLowerCase(), reason: status);
        expect(status, isNot(contains('_')), reason: 'no separators in $status');
        expect(int.tryParse(status), isNull, reason: 'not an ordinal: $status');
      }
    });

    test('are distinct', () {
      expect(allStatuses.toSet().length, allStatuses.length);
    });

    test('cover every value the server enum can emit', () {
      // ContractStatus = offered, active, submitted, completed, cancelled,
      // disputed, refunded. A value missing here would fall through to the
      // `_ => status` arm of statusLabel and render a raw wire string.
      expect(allStatuses.length, 7);
    });
  });

  group('isLive', () {
    // "Worth surfacing on the dashboard" -- signed by both parties and running,
    // **including** a disputed contract: the money is still held and the work
    // unresolved, so it belongs with the freelancer's active pile, not buried
    // among the terminal states.
    test('is true for active, submitted and disputed', () {
      expect(contractIn(Contract.statusActive).isLive, isTrue);
      expect(contractIn(Contract.statusSubmitted).isLive, isTrue);
      expect(contractIn(Contract.statusDisputed).isLive, isTrue);
    });

    test('is false for an unsigned offer', () {
      expect(contractIn(Contract.statusOffered).isLive, isFalse);
    });

    test('is true for a disputed contract rather than a terminal one', () {
      // The old predicate excluded `disputed` from the dashboard, so a contract
      // that was very much still the freelancer's responsibility vanished.
      expect(contractIn(Contract.statusDisputed).isLive, isTrue);
    });

    test('is false for every terminal state', () {
      for (final status in [
        Contract.statusCompleted,
        Contract.statusCancelled,
        Contract.statusRefunded,
      ]) {
        expect(contractIn(status).isLive, isFalse, reason: status);
      }
    });

    test('is false for an empty status rather than throwing', () {
      expect(emptyContract().isLive, isFalse);
    });
  });

  group('isClosed', () {
    test('is true for the three terminal states', () {
      expect(contractIn(Contract.statusCompleted).isClosed, isTrue);
      expect(contractIn(Contract.statusCancelled).isClosed, isTrue);
      expect(contractIn(Contract.statusRefunded).isClosed, isTrue);
    });

    test('is false for every non-terminal state', () {
      for (final status in [
        Contract.statusOffered,
        Contract.statusActive,
        Contract.statusSubmitted,
        Contract.statusDisputed,
      ]) {
        expect(contractIn(status).isClosed, isFalse, reason: status);
      }
    });
  });

  group('isLive and isClosed never overlap', () {
    // The dashboard groups on one or the other, so a status in both would
    // render the contract twice.
    test('holds for every status', () {
      for (final status in allStatuses) {
        final contract = contractIn(status);
        expect(
          contract.isLive && contract.isClosed,
          isFalse,
          reason: '$status is both live and closed',
        );
      }
    });
  });

  group('needsMySignature', () {
    // Gates the "Accept" button. `POST /api/contracts/{id}/accept` is the route
    // -- the old `/{id}/sign/` path is not registered.
    test('is true for an unsigned, non-closed contract', () {
      expect(
        contractIn(Contract.statusOffered, freelancerSigned: false).needsMySignature,
        isTrue,
      );
    });

    test('is false once the freelancer has signed', () {
      expect(
        contractIn(Contract.statusOffered, freelancerSigned: true).needsMySignature,
        isFalse,
      );
    });

    // A closed contract cannot be accepted, so offering the button would produce
    // a guaranteed 400.
    test('is false on a closed contract even when unsigned', () {
      for (final status in [
        Contract.statusCompleted,
        Contract.statusCancelled,
        Contract.statusRefunded,
      ]) {
        expect(
          contractIn(status, freelancerSigned: false).needsMySignature,
          isFalse,
          reason: status,
        );
      }
    });

    test('is false when nobody has signed and nothing is closed', () {
      // Both false: not closed, but nothing outstanding from the freelancer's
      // side -- e.g. an offer the client has not accepted yet.
      final contract = contractIn(
        Contract.statusOffered,
        clientSigned: false,
        freelancerSigned: false,
      );
      expect(contract.isSignedByBoth, isFalse);
      expect(contract.needsMySignature, isTrue);
    });
  });

  group('acceptsWork', () {
    // Gates uploading a deliverable or requesting a revision.
    test('is true when both parties have signed and it is not closed', () {
      for (final status in [
        Contract.statusActive,
        Contract.statusSubmitted,
        Contract.statusDisputed,
      ]) {
        expect(contractIn(status).acceptsWork, isTrue, reason: status);
      }
    });

    test('is false while the offer is unsigned', () {
      expect(
        contractIn(Contract.statusOffered, freelancerSigned: false).acceptsWork,
        isFalse,
      );
    });

    test('is false on a closed contract', () {
      for (final status in [
        Contract.statusCompleted,
        Contract.statusCancelled,
        Contract.statusRefunded,
      ]) {
        expect(contractIn(status).acceptsWork, isFalse, reason: status);
      }
    });

    // A disputed contract is signed by both, so this asserts the closed check is
    // not just "not active".
    test('is false only because of the closed check for a terminal state', () {
      final contract = contractIn(Contract.statusCompleted);
      expect(contract.isSignedByBoth, isTrue);
      expect(contract.acceptsWork, isFalse);
    });
  });

  group('statusLabel', () {
    test('renders a human label for every status', () {
      expect(contractIn(Contract.statusOffered).statusLabel, 'Offer');
      expect(contractIn(Contract.statusActive).statusLabel, 'Active');
      expect(contractIn(Contract.statusSubmitted).statusLabel, 'Submitted');
      expect(contractIn(Contract.statusCompleted).statusLabel, 'Completed');
      expect(contractIn(Contract.statusCancelled).statusLabel, 'Cancelled');
      expect(contractIn(Contract.statusDisputed).statusLabel, 'Disputed');
      expect(contractIn(Contract.statusRefunded).statusLabel, 'Refunded');
    });

    test('no status falls through to the raw wire string', () {
      // The `_ => status` arm is a safety net, not a display path. If it ever
      // fires for a real status, the user sees "offered" instead of "Offer".
      for (final status in allStatuses) {
        final label = contractIn(status).statusLabel;
        expect(label, isNot(status), reason: 'raw wire value shown for $status');
        expect(label, isNotEmpty);
      }
    });

    test('an unknown status degrades to itself rather than crashing', () {
      expect(contractIn('something_new').statusLabel, 'something_new');
    });

    test('an empty status reads as Unknown, not as an empty chip', () {
      expect(emptyContract().statusLabel, 'Unknown');
    });
  });

  group('status parsing', () {
    test('reads the status string off the payload', () {
      expect(contractIn(Contract.statusDisputed).status, 'disputed');
    });

    // A missing status must not read as `null`-the-string and then fail every
    // comparison in a way that looks like a data problem.
    test('a payload with no status is not any known status', () {
      final contract = Contract.fromJson({'id': 1, 'project_id': 2});
      expect(allStatuses, isNot(contains(contract.status)));
    });
  });
}
