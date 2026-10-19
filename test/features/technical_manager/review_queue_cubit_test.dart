import 'dart:async';

import 'package:flutter_test/flutter_test.dart';
import 'package:inspecta/core/enums/job_status.dart';
import 'package:inspecta/features/auth/domain/entities/user.dart';
import 'package:inspecta/features/certificate/domain/entities/certificate.dart';
import 'package:inspecta/features/certificate/domain/repositories/certificate_repository.dart';
import 'package:inspecta/features/certificate/domain/usecases/watch_certificate.dart';
import 'package:inspecta/features/coordinator/domain/repositories/coordinator_repository.dart';
import 'package:inspecta/features/coordinator/domain/usecases/get_inspectors.dart';
import 'package:inspecta/features/requests/domain/entities/inspection_request.dart';
import 'package:inspecta/features/requests/domain/usecases/get_requests.dart';
import 'package:inspecta/features/technical_manager/presentation/bloc/review_queue_cubit.dart';

import '../requests/fake_requests_repository.dart';

class _FakeCoordinatorRepository implements CoordinatorRepository {
  @override
  Stream<List<AppUser>> watchInspectors() => const Stream.empty();

  @override
  Future<void> markOnLeave(String inspectorId, DateTime? until) => throw UnimplementedError();
}

/// One controller per job, so a test can see which certificates were being
/// watched and when each was let go.
class _FakeCertificateRepository implements CertificateRepository {
  final controllers = <String, StreamController<Certificate?>>{};
  final cancelled = <String>{};

  @override
  Stream<Certificate?> watchCertificate(String requestId) {
    final controller = StreamController<Certificate?>(onCancel: () => cancelled.add(requestId));
    controllers[requestId] = controller;
    return controller.stream;
  }

  void submittedAt(String requestId, DateTime at) =>
      controllers[requestId]!.add(Certificate(requestId: requestId, inspectorId: 'insp-1', submittedAt: at));

  @override
  Stream<List<Certificate>> watchReviewedSince(DateTime since) => throw UnimplementedError();

  @override
  Future<void> saveDraft(Certificate certificate) => throw UnimplementedError();

  @override
  Future<void> submit(Certificate certificate) => throw UnimplementedError();

  @override
  Future<void> approve(Certificate certificate) => throw UnimplementedError();

  @override
  Future<void> sendBack(Certificate certificate, {required String note}) => throw UnimplementedError();
}

InspectionRequest _job(String id, {JobStatus status = JobStatus.certificateSubmitted, DateTime? scheduled, DateTime? submitted}) =>
    sampleRequest(id: id, status: status, scheduledAt: scheduled, certificateSubmittedAt: submitted);

List<String> _ids(List<InspectionRequest> jobs) => jobs.map((j) => j.id).toList();

void main() {
  late FakeRequestsRepository requestsRepo;
  late _FakeCertificateRepository certificateRepo;
  late ReviewQueueCubit cubit;

  setUp(() {
    requestsRepo = FakeRequestsRepository();
    certificateRepo = _FakeCertificateRepository();
    cubit = ReviewQueueCubit(
      GetRequests(requestsRepo),
      GetInspectors(_FakeCoordinatorRepository()),
      WatchCertificate(certificateRepo),
    )..start();
  });

  tearDown(() => cubit.close());

  test('a job submitted before requests were timed is ordered by its certificate\'s submission time', () async {
    requestsRepo.controller.add([
      _job('scheduled-late', scheduled: DateTime(2026, 10, 9)),
      _job('scheduled-early', scheduled: DateTime(2026, 10, 2)),
    ]);
    await pumpEventQueue();
    // Until the certificates answer, it's the scheduled order.
    expect(_ids(cubit.state.toReview), ['scheduled-late', 'scheduled-early']);

    certificateRepo.submittedAt('scheduled-late', DateTime(2026, 10, 1, 9));
    certificateRepo.submittedAt('scheduled-early', DateTime(2026, 10, 3, 9));
    await pumpEventQueue();

    expect(_ids(cubit.state.toReview), ['scheduled-early', 'scheduled-late']);
  });

  test('a legacy job mixes in correctly with ones whose request carries the time', () async {
    requestsRepo.controller.add([
      _job('legacy', scheduled: DateTime(2026, 10, 9)),
      _job('timed', submitted: DateTime(2026, 10, 5)),
    ]);
    await pumpEventQueue();
    certificateRepo.submittedAt('legacy', DateTime(2026, 10, 7));
    await pumpEventQueue();

    expect(_ids(cubit.state.toReview), ['legacy', 'timed']);
  });

  test('only jobs without a recorded time, still in the queue, have their certificate watched', () async {
    requestsRepo.controller.add([
      _job('legacy'),
      _job('timed', submitted: DateTime(2026, 10, 5)),
      _job('returned', status: JobStatus.certificateReturned),
      _job('sent', status: JobStatus.sentToClient),
      _job('assigned', status: JobStatus.assigned),
    ]);
    await pumpEventQueue();

    expect(certificateRepo.controllers.keys, unorderedEquals(['legacy', 'returned']));
  });

  test('stops watching a certificate once its job leaves the queue', () async {
    requestsRepo.controller.add([_job('legacy-a'), _job('legacy-b')]);
    await pumpEventQueue();
    expect(certificateRepo.cancelled, isEmpty);

    requestsRepo.controller.add([_job('legacy-a'), _job('legacy-b', status: JobStatus.sentToClient)]);
    await pumpEventQueue();

    expect(certificateRepo.cancelled, {'legacy-b'});
  });

  test('a certificate that can\'t be read leaves the job in the scheduled order', () async {
    requestsRepo.controller.add([
      _job('unreadable', scheduled: DateTime(2026, 10, 9)),
      _job('plain', scheduled: DateTime(2026, 10, 2)),
    ]);
    await pumpEventQueue();

    certificateRepo.controllers['unreadable']!.addError(Exception('permission-denied'));
    await pumpEventQueue();

    expect(_ids(cubit.state.toReview), ['unreadable', 'plain']);
  });
}
