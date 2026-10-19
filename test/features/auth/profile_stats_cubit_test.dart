import 'dart:async';

import 'package:flutter_test/flutter_test.dart';
import 'package:inspecta/core/enums/job_status.dart';
import 'package:inspecta/features/auth/domain/entities/user.dart';
import 'package:inspecta/features/auth/domain/entities/user_role.dart';
import 'package:inspecta/features/auth/presentation/bloc/profile_stats_cubit.dart';
import 'package:inspecta/features/certificate/domain/entities/certificate.dart';
import 'package:inspecta/features/certificate/domain/repositories/certificate_repository.dart';
import 'package:inspecta/features/certificate/domain/usecases/watch_inspector_certificates.dart';
import 'package:inspecta/features/certificate/domain/usecases/watch_reviewed_certificates.dart';
import 'package:inspecta/features/clients/domain/entities/client.dart';
import 'package:inspecta/features/clients/domain/repositories/clients_repository.dart';
import 'package:inspecta/features/clients/domain/usecases/get_clients.dart';
import 'package:inspecta/features/quotation/domain/usecases/get_quotations.dart';
import 'package:inspecta/features/requests/domain/usecases/get_my_tasks.dart';
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
  final mine = StreamController<List<Certificate>>.broadcast();
  final inspectorCalls = <String>[];

  @override
  Stream<List<Certificate>> watchInspectorCertificates(String inspectorId) {
    inspectorCalls.add(inspectorId);
    return mine.stream;
  }

  @override
  Stream<List<Certificate>> watchReviewedSince(DateTime since) {
    sinceCalls.add(since);
    return reviewed.stream;
  }
}

AppUser _user(UserRole role) => AppUser(id: 'insp-1', name: 'Karim', email: 'k@x.test', role: role);

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
      GetMyTasks(requestsRepo),
      WatchInspectorCertificates(certificateRepo),
    );
  });

  tearDown(() => cubit.close());

  group('a technical manager', () {
    setUp(() => cubit.start(_user(UserRole.technicalManager)));

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

  group('an inspector', () {
    final now = DateTime.now();
    DateTime thisMonth(int day) => DateTime(now.year, now.month, day.clamp(1, 28));
    final lastMonth = DateTime(now.year, now.month - 1, 15);

    Certificate submitted(String id, DateTime at, {DateTime? reviewedAt}) =>
        Certificate(requestId: id, inspectorId: 'insp-1', submittedAt: at, reviewedAt: reviewedAt);

    setUp(() => cubit.start(_user(UserRole.inspector)));

    test('reads only their own jobs and certificates', () {
      expect(certificateRepo.inspectorCalls, ['insp-1']);
      expect(certificateRepo.sinceCalls, isEmpty);
    });

    test('counts the inspections started this month, not ones only assigned or from another month', () async {
      requestsRepo.assignedController.add([
        sampleRequest(id: 'underway', status: JobStatus.inProgress, scheduledAt: thisMonth(3)),
        sampleRequest(id: 'certified', status: JobStatus.certificateSubmitted, scheduledAt: thisMonth(4)),
        sampleRequest(id: 'sent', status: JobStatus.sentToClient, scheduledAt: thisMonth(5)),
        sampleRequest(id: 'not-started', status: JobStatus.taskAccepted, scheduledAt: thisMonth(6)),
        sampleRequest(id: 'old', status: JobStatus.sentToClient, scheduledAt: lastMonth),
      ]);
      await pumpEventQueue();

      expect(cubit.state.inspectionsThisMonth, 3);
    });

    test('counts the certificates handed in this month', () async {
      certificateRepo.mine.add([
        submitted('a', thisMonth(2)),
        submitted('b', thisMonth(9)),
        submitted('old', lastMonth),
        const Certificate(requestId: 'draft', inspectorId: 'insp-1'),
      ]);
      await pumpEventQueue();

      expect(cubit.state.certificatesThisMonth, 2);
    });

    test('counts what was sent back this month, while it is still returned or has been resubmitted', () async {
      requestsRepo.assignedController.add([
        sampleRequest(id: 'returned', status: JobStatus.certificateReturned),
        sampleRequest(id: 'resubmitted', status: JobStatus.certificateSubmitted),
        sampleRequest(id: 'approved', status: JobStatus.sentToClient),
        sampleRequest(id: 'old-return', status: JobStatus.certificateReturned),
      ]);
      certificateRepo.mine.add([
        submitted('returned', lastMonth, reviewedAt: thisMonth(7)),
        submitted('resubmitted', lastMonth, reviewedAt: thisMonth(8)),
        submitted('approved', lastMonth, reviewedAt: thisMonth(9)),
        submitted('old-return', lastMonth, reviewedAt: lastMonth),
      ]);
      await pumpEventQueue();

      expect(cubit.state.returnedThisMonth, 2);
    });

    test('is zero, not blank, when nothing has happened this month', () async {
      requestsRepo.assignedController.add(const []);
      certificateRepo.mine.add(const []);
      await pumpEventQueue();

      expect((cubit.state.inspectionsThisMonth, cubit.state.certificatesThisMonth, cubit.state.returnedThisMonth), (0, 0, 0));
    });
  });

  test('other roles don\'t read the reviewed certificates', () async {
    for (final role in [UserRole.coordinator, UserRole.inspector, UserRole.admin, null]) {
      final other = ProfileStatsCubit(
        GetQuotations(FakeQuotationRepository()),
        GetClients(_FakeClientsRepository()),
        GetRequests(requestsRepo),
        WatchReviewedCertificates(certificateRepo),
        GetMyTasks(requestsRepo),
        WatchInspectorCertificates(certificateRepo),
      )..start(role == null ? null : AppUser(id: 'u', name: 'U', email: 'u@x.test', role: role));
      await other.close();
    }

    expect(certificateRepo.sinceCalls, isEmpty);
  });
}
