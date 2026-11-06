import 'package:dio/dio.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:inspecta/features/machines/data/datasources/machines_api_client_services.dart';
import 'package:inspecta/features/machines/data/repositories/machines_repository_impl.dart';
import 'package:inspecta/features/machines/domain/entities/certificate_incomplete_failure.dart';
import 'package:inspecta/features/machines/domain/entities/job_machines.dart';
import 'package:inspecta/features/machines/domain/repositories/machines_repository.dart';
import 'package:inspecta/features/machines/domain/usecases/get_job_machines.dart';
import 'package:inspecta/features/machines/domain/usecases/start_job_inspection.dart';
import 'package:inspecta/features/machines/domain/usecases/submit_all_machines.dart';
import 'package:inspecta/features/machines/presentation/bloc/job_machines_cubit.dart';

class _Api extends Fake implements MachinesApiClientServices {
  String? submittedJob;
  Object? failure;

  @override
  Future<dynamic> submitAll(String requestId) async {
    submittedJob = requestId;
    if (failure != null) throw failure!;
    return null;
  }
}

DioException _conflict(Object body) {
  final options = RequestOptions(path: 'quotation-queue/job-1/submit-all');
  return DioException(
    requestOptions: options,
    type: DioExceptionType.badResponse,
    response: Response(requestOptions: options, statusCode: 409, data: body),
  );
}

class _Repo extends Fake implements MachinesRepository {
  Object? submitFailure;
  int submitCalls = 0;
  int loads = 0;

  @override
  Future<void> submitAllMachines(String requestId) async {
    submitCalls++;
    if (submitFailure != null) throw submitFailure!;
  }

  @override
  Future<JobMachines> getMachines(String requestId) async {
    loads++;
    return JobMachines(
      requestId: requestId,
      requestStatus: 'certificate_submitted',
      clientName: 'Al Noor',
      location: 'Cairo',
      progress: const MachineProgress(total: 2, submitted: 2),
      machines: const [],
    );
  }
}

JobMachinesCubit _cubit(_Repo repo) => JobMachinesCubit(
  requestId: 'job-1',
  getMachines: GetJobMachines(repo),
  startInspection: StartJobInspection(repo),
  submitAll: SubmitAllMachines(repo),
);

void main() {
  group('sending the whole job through the repository', () {
    test('calls submit-all for the job', () async {
      final api = _Api();

      await MachinesRepositoryImpl(api).submitAllMachines('job-1');

      expect(api.submittedJob, 'job-1');
    });

    test('a 409 certificate_incomplete carries what each machine is missing', () async {
      final api = _Api()
        ..failure = _conflict({
          'code': 'certificate_incomplete',
          'problems': ['Crane: the certificate is not filled in', 'Forklift: standard of inspection'],
        });

      await expectLater(
        MachinesRepositoryImpl(api).submitAllMachines('job-1'),
        throwsA(
          isA<CertificateIncompleteFailure>().having(
            (f) => f.problems,
            'problems',
            ['Crane: the certificate is not filled in', 'Forklift: standard of inspection'],
          ),
        ),
      );
    });
  });

  group('the machines screen', () {
    test('a sent job is reloaded and reported as sent', () async {
      final repo = _Repo();
      final cubit = _cubit(repo);

      final outcome = await cubit.submitAll();

      expect(outcome.sent, isTrue);
      expect(repo.loads, 1);
      expect(cubit.state.isSubmitting, isFalse);
      await cubit.close();
    });

    test('a job that is not ready reports the problems and sends nothing', () async {
      final repo = _Repo()..submitFailure = const CertificateIncompleteFailure(['Crane: not filled in']);
      final cubit = _cubit(repo);

      final outcome = await cubit.submitAll();

      expect(outcome.sent, isFalse);
      expect(outcome.problems, ['Crane: not filled in']);
      expect(repo.loads, 0);
      expect(cubit.state.isSubmitting, isFalse);
      await cubit.close();
    });

    test('any other failure is reported by its code', () async {
      final repo = _Repo()..submitFailure = StateError('boom');
      final cubit = _cubit(repo);

      final outcome = await cubit.submitAll();

      expect(outcome.sent, isFalse);
      expect(outcome.problems, isEmpty);
      expect(outcome.failure, isNotNull);
      await cubit.close();
    });
  });
}
