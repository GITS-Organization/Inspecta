import 'dart:async';

import 'package:flutter_test/flutter_test.dart';
import 'package:inspecta/core/enums/job_status.dart';
import 'package:inspecta/features/auth/domain/entities/user_role.dart';
import 'package:inspecta/features/auth/presentation/bloc/profile_stats_cubit.dart';
import 'package:inspecta/features/certificate/domain/entities/certificate.dart';
import 'package:inspecta/features/certificate/domain/repositories/certificate_repository.dart';
import 'package:inspecta/features/certificate/domain/usecases/watch_reviewed_certificates.dart';
import 'package:inspecta/features/clients/domain/entities/client.dart';
import 'package:inspecta/features/clients/domain/repositories/clients_repository.dart';
import 'package:inspecta/features/clients/domain/usecases/get_clients.dart';
import 'package:inspecta/features/quotation/domain/usecases/get_quotations.dart';
import 'package:inspecta/features/requests/domain/usecases/get_requests.dart';

import '../quotation/fake_quotation_repository.dart';
import '../requests/fake_requests_repository.dart';

class _FakeClientsRepository extends Fake implements ClientsRepository {
  @override
  Stream<List<Client>> watchClients() => const Stream.empty();
}

class _FakeCertificateRepository extends Fake implements CertificateRepository {
  final reviewed = StreamController<List<Certificate>>.broadcast();
  final sinceCalls = <DateTime>[];

  @override
  Stream<List<Certificate>> watchReviewedSince(DateTime since) {
    sinceCalls.add(since);
    return reviewed.stream;
  }
}

Certificate _reviewedCertificate(String requestId) =>
    Certificate(requestId: requestId, inspectorId: 'insp-1', reviewedAt: DateTime.now());

void main() {
  late FakeRequestsRepository requestsRepo;
  late _FakeCertificateRepository certificateRepo;
  late ProfileStatsCubit cubit;

  setUp(() {
    requestsRepo = FakeRequestsRepository();
    certificateRepo = _FakeCertificateRepository();
    cubit = ProfileStatsCubit(
      GetQuotations(FakeQuotationRepository()),
      GetClients(_FakeClientsRepository()),
      GetRequests(requestsRepo),
      WatchReviewedCertificates(certificateRepo),
    );
  });

  tearDown(() => cubit.close());

  group('a technical manager', () {
    setUp(() => cubit.start(UserRole.technicalManager));

    test('has no figures until both the jobs and the reviewed certificates have arrived', () async {
      requestsRepo.controller.add([sampleRequest(id: 'a', status: JobStatus.sentToClient)]);
      await pumpEventQueue();
      expect(cubit.state.reviewedThisMonth, isNull);

      certificateRepo.reviewed.add([_reviewedCertificate('a')]);
      await pumpEventQueue();
      expect(cubit.state.reviewedThisMonth, 1);
    });

    test('counts what was approved as sent, and what went back to the inspector as returned', () async {
      requestsRepo.controller.add([
        sampleRequest(id: 'sent-1', status: JobStatus.sentToClient),
        sampleRequest(id: 'sent-2', status: JobStatus.sentToClient),
        sampleRequest(id: 'returned', status: JobStatus.certificateReturned),
        // Sent back, then handed in again: still a return made this month.
        sampleRequest(id: 'resubmitted', status: JobStatus.certificateSubmitted),
      ]);
      certificateRepo.reviewed.add([
        _reviewedCertificate('sent-1'),
        _reviewedCertificate('sent-2'),
        _reviewedCertificate('returned'),
        _reviewedCertificate('resubmitted'),
      ]);
      await pumpEventQueue();

      expect(cubit.state.sentThisMonth, 2);
      expect(cubit.state.returnedThisMonth, 2);
      expect(cubit.state.reviewedThisMonth, 4);
    });

    test('ignores a certificate whose job is unknown or hasn\'t been through review', () async {
      requestsRepo.controller.add([sampleRequest(id: 'in-progress', status: JobStatus.inProgress)]);
      certificateRepo.reviewed.add([_reviewedCertificate('in-progress'), _reviewedCertificate('missing')]);
      await pumpEventQueue();

      expect(cubit.state.reviewedThisMonth, 0);
      expect(cubit.state.returnedThisMonth, 0);
      expect(cubit.state.sentThisMonth, 0);
    });

    test('is zero, not blank, in a month with no reviews', () async {
      requestsRepo.controller.add([sampleRequest(id: 'a', status: JobStatus.certificateSubmitted)]);
      certificateRepo.reviewed.add(const []);
      await pumpEventQueue();

      expect(cubit.state.reviewedThisMonth, 0);
    });

    test('moves with the jobs, as a certificate is approved', () async {
      certificateRepo.reviewed.add([_reviewedCertificate('a')]);
      requestsRepo.controller.add([sampleRequest(id: 'a', status: JobStatus.certificateReturned)]);
      await pumpEventQueue();
      expect((cubit.state.returnedThisMonth, cubit.state.sentThisMonth), (1, 0));

      requestsRepo.controller.add([sampleRequest(id: 'a', status: JobStatus.sentToClient)]);
      await pumpEventQueue();
      expect((cubit.state.returnedThisMonth, cubit.state.sentThisMonth), (0, 1));
    });

    test('asks only for the certificates reviewed since the start of this month', () {
      final now = DateTime.now();
      expect(certificateRepo.sinceCalls, [DateTime(now.year, now.month)]);
    });

    test('a failing certificates read leaves the tiles blank rather than wrong', () async {
      requestsRepo.controller.add([sampleRequest(id: 'a', status: JobStatus.sentToClient)]);
      certificateRepo.reviewed.addError(Exception('permission-denied'));
      await pumpEventQueue();

      expect(cubit.state.reviewedThisMonth, isNull);
    });
  });

  test('other roles don\'t read the reviewed certificates', () async {
    for (final role in [UserRole.coordinator, UserRole.inspector, UserRole.admin, null]) {
      final other = ProfileStatsCubit(
        GetQuotations(FakeQuotationRepository()),
        GetClients(_FakeClientsRepository()),
        GetRequests(requestsRepo),
        WatchReviewedCertificates(certificateRepo),
      )..start(role);
      await other.close();
    }

    expect(certificateRepo.sinceCalls, isEmpty);
  });
}
