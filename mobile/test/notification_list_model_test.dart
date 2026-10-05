import 'package:flutter_test/flutter_test.dart';
import 'package:webofwork/models/notification_list_model.dart';

void main() {
  group('NotificationListModel envelope', () {
    // `GET /api/notifications` answers
    // `{"data": {"unread_count": n, "total_count": n, "items": [...]}}`.
    // The template read `data` as a *list*, so the notifications list rendered
    // empty even on a 200, and the badge had no source of truth.
    test('reads the data envelope and both counters', () {
      final model = NotificationListModel.fromJson({
        'data': {
          'unread_count': 2,
          'total_count': 7,
          'items': [
            {
              'id': 1,
              'type': 'proposal_accepted',
              'message': 'Your proposal was accepted',
              'is_read': false,
              'created_at': '2026-09-20T08:00:00.000Z',
              'related_entity_type': 'proposal',
              'related_entity_id': 55,
            },
            {
              'id': 2,
              'type': 'delivered',
              'message': 'You delivered a file',
              'is_read': true,
              'related_entity_type': 'contract',
              'related_entity_id': 9,
            },
          ],
        },
      });

      expect(model.unreadCount, 2);
      expect(model.totalCount, 7);
      expect(model.notifications, hasLength(2));
      expect(model.notifications!.first.id, 1);
      expect(model.notifications!.last.isRead, isTrue);
    });

    test('tolerates a bare array under data', () {
      final model = NotificationListModel.fromJson({
        'data': [
          {'id': 3, 'type': 'new_message', 'message': 'Hi', 'is_read': false},
        ],
      });

      expect(model.notifications, hasLength(1));
      // Counters default rather than throwing when the envelope is absent.
      expect(model.unreadCount, 0);
      expect(model.totalCount, 0);
    });

    test('a 204-shaped or empty body reports nothing instead of throwing', () {
      // `returnResponse` hands a 204 back as an empty map.
      final model = NotificationListModel.fromJson(<String, dynamic>{});

      expect(model.notifications, isEmpty);
      expect(model.unreadCount, 0);
    });

    test('coerces string counters and ids', () {
      final model = NotificationListModel.fromJson({
        'data': {
          'unread_count': '4',
          'total_count': '9',
          'items': [
            {'id': '11', 'type': 'new_message', 'message': 'x', 'is_read': false},
          ],
        },
      });

      expect(model.unreadCount, 4);
      expect(model.totalCount, 9);
      expect(model.notifications!.single.id, 11);
    });
  });

  group('AppNotification.is_read', () {
    // `bool IsRead` on the backend. The template compared against the strings
    // "read"/"unread", which never matched, so every row read as unread and the
    // badge never drained.
    test('reads the bool the API sends', () {
      expect(
        AppNotification.fromJson({'id': 1, 'type': 't', 'message': 'm', 'is_read': true})
            .isRead,
        isTrue,
      );
      expect(
        AppNotification.fromJson({'id': 1, 'type': 't', 'message': 'm', 'is_read': false})
            .isRead,
        isFalse,
      );
    });

    test('tolerates the legacy read/unread strings and 0/1', () {
      expect(AppNotification.fromJson({'id': 1, 'is_read': 'read'}).isRead, isTrue);
      expect(AppNotification.fromJson({'id': 1, 'is_read': 'unread'}).isRead, isFalse);
      expect(AppNotification.fromJson({'id': 1, 'is_read': 1}).isRead, isTrue);
      expect(AppNotification.fromJson({'id': 1, 'is_read': 0}).isRead, isFalse);
    });

    test('a missing flag is unread, so the row still shows up', () {
      expect(AppNotification.fromJson({'id': 1, 'type': 't'}).isRead, isFalse);
    });
  });

  group('AppNotification type', () {
    test('prefers type, which both the list and the socket send', () {
      final n = AppNotification.fromJson({
        'id': 1,
        'type': 'contract_completed',
        'notification_type': 'legacy',
      });

      expect(n.type, 'contract_completed');
    });

    // `notification_type` was the pre-change push field name. Accepting it means a
    // client reconnecting to a not-yet-restarted backend does not render every
    // push as "unknown".
    test('falls back to notification_type, then to unknown', () {
      expect(
        AppNotification.fromJson({'id': 1, 'notification_type': 'new_message'}).type,
        'new_message',
      );
      expect(AppNotification.fromJson({'id': 1}).type, 'unknown');
    });

    // The enum is open-ended (19 values in NotificationWireFormat.cs). The old
    // `typeValues.map[json["type"]]!` used a null assertion and threw on any
    // unrecognised value, taking down the whole list.
    test('an unknown type is kept verbatim rather than throwing', () {
      final n = AppNotification.fromJson({'id': 1, 'type': 'a_brand_new_event'});

      expect(n.type, 'a_brand_new_event');
    });
  });

  group('AppNotification deep-link fields', () {
    test('reads related_entity_type and related_entity_id', () {
      final n = AppNotification.fromJson({
        'id': 1,
        'type': 'delivered',
        'related_entity_type': 'contract',
        'related_entity_id': 12,
      });

      expect(n.relatedEntityType, 'contract');
      expect(n.relatedEntityId, 12);
    });

    // The old push shape carried `related_id` and no `related_entity_type` at
    // all, so a push told the client *that* something happened but never *what*,
    // and tapping it could not navigate. Reading both keeps an older backend's
    // push deep-linkable.
    test('falls back to the old push related_id', () {
      final n = AppNotification.fromJson({
        'id': 1,
        'type': 'new_message',
        'related_id': 33,
      });

      expect(n.relatedEntityId, 33);
    });

    test('a null related id does not become zero, which would deep-link wrongly', () {
      final n = AppNotification.fromJson({
        'id': 1,
        'type': 'new_message',
        'related_entity_type': 'message',
        'related_entity_id': null,
        'related_id': null,
      });

      expect(n.relatedEntityId, isNull);
      expect(n.relatedEntityType, 'message');
    });

    test('a non-numeric related id is reported as absent, not as 0', () {
      final n = AppNotification.fromJson({'id': 1, 'related_entity_id': 'not-a-number'});

      expect(n.relatedEntityId, isNull);
    });
  });

  group('AppNotification round trip', () {
    test('toJson emits the snake_case keys the API expects', () {
      final json = AppNotification(
        id: 4,
        type: 'proposal_accepted',
        message: 'Accepted',
        isRead: false,
        relatedEntityType: 'proposal',
        relatedEntityId: 8,
      ).toJson();

      expect(json['type'], 'proposal_accepted');
      expect(json['is_read'], isFalse);
      expect(json['related_entity_type'], 'proposal');
      expect(json['related_entity_id'], 8);
    });

    test('a list response and a push of the same row parse identically', () {
      // NotificationRealtimeBinder folds socket pushes into the same list, so the
      // two shapes have to agree or a pushed row would render differently from
      // the same row refetched over REST.
      final fromList = AppNotification.fromJson({
        'id': 8,
        'type': 'delivered',
        'message': 'Delivered',
        'is_read': false,
        'created_at': '2026-09-20T08:00:00.000Z',
        'related_entity_type': 'contract',
        'related_entity_id': 3,
      });
      final fromPush = AppNotification.fromJson({
        'id': 8,
        'type': 'delivered',
        'message': 'Delivered',
        'is_read': false,
        'created_at': '2026-09-20T08:00:00.000Z',
        'related_entity_type': 'contract',
        'related_entity_id': 3,
      });

      expect(fromPush.toJson(), fromList.toJson());
    });
  });
}
