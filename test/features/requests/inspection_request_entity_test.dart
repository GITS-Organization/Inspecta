import 'package:flutter_test/flutter_test.dart';
import 'package:inspecta/core/enums/job_status.dart';
import 'package:inspecta/features/requests/domain/entities/request_item.dart';
import 'package:inspecta/features/requests/domain/entities/requests_tab.dart';

import 'fake_requests_repository.dart';

void main() {
  group('RequestsTabX.isNew (BR-02.7)', () {
    test('only an unquoted request counts as New', () {
      expect(RequestsTabX.isNew(JobStatus.requestReceived), isTrue);
      expect(RequestsTabX.isNew(JobStatus.quoteDraft), isTrue);
      expect(RequestsTabX.isNew(JobStatus.quoteSent), isFalse);
      expect(RequestsTabX.isNew(JobStatus.clientCountered), isFalse);
      expect(RequestsTabX.isNew(JobStatus.quoteRejected), isFalse);
      expect(RequestsTabX.isNew(JobStatus.quoteAccepted), isFalse);
      expect(RequestsTabX.isNew(JobStatus.sentToClient), isFalse);
    });
  });

  group('InspectionRequest', () {
    test('equipmentTitle uses the first item', () {
      final request = sampleRequest();
      expect(request.equipmentTitle, 'Overhead crane — 10 t');
    });

    test('totalUnits sums every item\'s quantity', () {
      final request = sampleRequest(
        items: const [
          RequestItem(id: '1', type: 'Crane', quantity: 2),
          RequestItem(id: '2', type: 'Forklift', quantity: 1),
        ],
      );
      expect(request.totalUnits, 3);
    });

    test('isWaitingOver24h only in the New tab, past 24h (BR-02.8)', () {
      final old = sampleRequest(
        status: JobStatus.requestReceived,
        receivedAt: DateTime(2026, 1, 1),
      );
      final now = DateTime(2026, 1, 2, 1);
      expect(old.isWaitingOver24h(now: now), isTrue);

      final fresh = sampleRequest(
        status: JobStatus.requestReceived,
        receivedAt: DateTime(2026, 1, 2),
      );
      expect(fresh.isWaitingOver24h(now: now), isFalse);

      final quoted = sampleRequest(
        status: JobStatus.quoteSent,
        receivedAt: DateTime(2026, 1, 1),
      );
      expect(quoted.isWaitingOver24h(now: now), isFalse);
    });

    test('isReadyToQuote requires a client, an item and a location (BR-02.5)', () {
      expect(sampleRequest().isReadyToQuote, isTrue);
      expect(sampleRequest(clientId: null).isReadyToQuote, isFalse);
      expect(sampleRequest(items: const []).isReadyToQuote, isFalse);
    });

    test('isQuotable: a first quote while New, or a new version after a counter', () {
      expect(sampleRequest(status: JobStatus.requestReceived).isQuotable, isTrue);
      expect(sampleRequest(status: JobStatus.quoteDraft).isQuotable, isTrue);
      expect(sampleRequest(status: JobStatus.clientCountered).isQuotable, isTrue);
      expect(sampleRequest(status: JobStatus.quoteSent).isQuotable, isFalse);
      expect(sampleRequest(status: JobStatus.quoteAccepted).isQuotable, isFalse);
      expect(sampleRequest(status: JobStatus.clientCountered, items: const []).isQuotable, isFalse);
    });

    test('isNewClient reflects a missing client match (BR-02.4)', () {
      expect(sampleRequest(clientId: null).isNewClient, isTrue);
      expect(sampleRequest().isNewClient, isFalse);
    });
  });
}
