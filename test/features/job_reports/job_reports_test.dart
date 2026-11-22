import 'dart:typed_data';

import 'package:flutter_test/flutter_test.dart';
import 'package:inspecta/features/coordinator/presentation/bloc/delivery_email_cubit.dart';
import 'package:inspecta/features/job_reports/data/models/job_reports_parser.dart';
import 'package:inspecta/features/job_reports/domain/entities/job_report.dart';
import 'package:inspecta/features/job_reports/domain/entities/job_report_failure.dart';
import 'package:inspecta/features/job_reports/domain/repositories/job_reports_repository.dart';
import 'package:inspecta/features/job_reports/domain/usecases/delete_job_report.dart';
import 'package:inspecta/features/job_reports/domain/usecases/download_job_report.dart';
import 'package:inspecta/features/job_reports/domain/usecases/download_job_reports_zip.dart';
import 'package:inspecta/features/job_reports/domain/usecases/get_job_reports.dart';
import 'package:inspecta/features/job_reports/domain/usecases/upload_job_report.dart';
import 'package:inspecta/features/job_reports/presentation/bloc/job_reports_cubit.dart';
import 'package:inspecta/features/machines/data/models/machines_parser.dart';
import 'package:inspecta/features/machines/domain/entities/delivery.dart';

/// A repository that holds one list and counts what reaches it.
class _FakeRepository implements JobReportsRepository {
  JobReportList current;
  int uploads = 0;

  _FakeRepository(this.current);

  @override
  Future<JobReportList> list(String requestId) async => current;

  @override
  Future<JobReport> upload(String requestId, JobReportUpload file) async {
    uploads++;
    return JobReport(id: 'new', fileName: file.fileName, sizeBytes: file.bytes.length);
  }

  @override
  Future<JobReportList> delete(String requestId, String fileId) async => current;

  @override
  Future<Uint8List> download(String requestId, String fileId) async => Uint8List(0);

  @override
  Future<Uint8List> downloadAll(String requestId) async => Uint8List(0);
}

JobReportsCubit _cubit(_FakeRepository repository) => JobReportsCubit(
  requestId: 'r1',
  getJobReports: GetJobReports(repository),
  uploadJobReport: UploadJobReport(repository),
  deleteJobReport: DeleteJobReport(repository),
  downloadJobReport: DownloadJobReport(repository),
  downloadJobReportsZip: DownloadJobReportsZip(repository),
);

void main() {
  test('the report list reads files, limits and expiry', () {
    final list = JobReportsParser.list({
      'files': [
        {
          'id': 'f1',
          'fileName': 'Weld report.pdf',
          'sizeBytes': 2048,
          'machineName': 'Crane',
          'uploadedBy': 'Karim',
          'uploadedAt': '2026-05-01T08:00:00Z',
          'canDelete': true,
        },
      ],
      'totalBytes': 2048,
      'canUpload': true,
      'maxFiles': 20,
      'maxFileBytes': 10485760,
      'maxTotalBytes': 31457280,
      'expiresAt': '2026-06-01T00:00:00Z',
    });

    expect(list.files.single.fileName, 'Weld report.pdf');
    expect(list.files.single.canDelete, isTrue);
    expect(list.canUpload, isTrue);
    expect(list.expiresAt, DateTime.utc(2026, 6, 1));
  });

  group('adding a file', () {
    final empty = JobReportList(canUpload: true);

    Future<JobReportFailureCode?> add(_FakeRepository repository, String name, int size) async {
      final cubit = _cubit(repository);
      await cubit.start();
      final failure = await cubit.upload(fileName: name, bytes: Uint8List(size));
      await cubit.close();
      return failure;
    }

    test('goes up when it is allowed', () async {
      final repository = _FakeRepository(empty);
      expect(await add(repository, 'Scan.PDF', 10), isNull);
      expect(repository.uploads, 1);
    });

    test('is refused here, before sending, when the type or size is wrong', () async {
      final repository = _FakeRepository(empty);
      expect(await add(repository, 'photo.heic', 10), JobReportFailureCode.heic);
      expect(await add(repository, 'all.zip', 10), JobReportFailureCode.unsupportedType);
      expect(await add(repository, 'noextension', 10), JobReportFailureCode.unsupportedType);
      expect(await add(repository, 'empty.pdf', 0), JobReportFailureCode.emptyFile);
      expect(await add(repository, 'big.pdf', JobReportList.defaultMaxFileBytes + 1), JobReportFailureCode.fileTooLarge);
      expect(repository.uploads, 0);
    });

    test('is refused when the job is full', () async {
      final full = JobReportList(
        canUpload: true,
        files: List.generate(20, (i) => JobReport(id: '$i', fileName: '$i.pdf', sizeBytes: 1)),
        totalBytes: 20,
      );
      final repository = _FakeRepository(full);
      expect(await add(repository, 'one-more.pdf', 10), JobReportFailureCode.limitReached);
      expect(repository.uploads, 0);
    });
  });

  group('the delivery preview', () {
    final preview = MachinesParser.deliveryPreview({
      'to': 'client@example.com',
      'subject': 'Certificates',
      'body': 'Hello',
      'canSend': false,
      'blockers': ['The reports are too large to email together with the certificates. Untick some reports.'],
      'certificates': [
        {'number': 'C-1', 'sizeBytes': 10 * 1024 * 1024},
      ],
      'reports': [
        {'id': 'a', 'fileName': 'a.pdf', 'sizeBytes': 6 * 1024 * 1024, 'machineName': 'Crane'},
        {'id': 'b', 'fileName': 'b.pdf', 'sizeBytes': 3 * 1024 * 1024},
      ],
      'maxEmailBytes': 17825792,
    });

    test('reads the reports and the limit', () {
      expect(preview.reports.map((r) => r.id), ['a', 'b']);
      expect(preview.certificates.single.sizeBytes, 10 * 1024 * 1024);
      expect(preview.maxEmailBytes, 17825792);
      expect(preview.hardBlockers, isEmpty);
      expect(preview.reportBlockers, hasLength(1));
    });

    test('the "too large" blocker does not stop Send, and unticking lowers the total', () {
      final state = DeliveryEmailState(preview: preview, to: 'client@example.com');
      expect(state.canSend, isTrue);
      expect(state.emailBytes, 19 * 1024 * 1024);
      expect(state.isOverSizeLimit, isTrue);

      final trimmed = state.copyWith(excludedReportIds: {'a'});
      expect(trimmed.includedReports.map((r) => r.id), ['b']);
      expect(trimmed.emailBytes, 13 * 1024 * 1024);
      expect(trimmed.isOverSizeLimit, isFalse);

      final none = state.copyWith(excludedReportIds: {'a', 'b'});
      expect(none.isOverSizeLimit, isFalse);
    });

    test('any other blocker still stops Send', () {
      final blocked = DeliveryEmailState(
        preview: MachinesParser.deliveryPreview({
          'canSend': false,
          'blockers': ['No certificate is ready.'],
        }),
        to: 'client@example.com',
      );
      expect(blocked.canSend, isFalse);
    });
  });
}
