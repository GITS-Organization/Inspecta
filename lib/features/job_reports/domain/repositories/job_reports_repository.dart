import 'dart:typed_data';

import '../entities/job_report.dart';

/// Methods throw [JobReportFailure] on expected errors.
abstract interface class JobReportsRepository {
  /// The job's live reports, with the limits and what this user may do.
  Future<JobReportList> list(String requestId);

  /// Adds one file; answers with the file as stored.
  Future<JobReport> upload(String requestId, JobReportUpload file);

  /// Removes a file; answers with the new list.
  Future<JobReportList> delete(String requestId, String fileId);

  /// One file's bytes.
  Future<Uint8List> download(String requestId, String fileId);

  /// Every live report of the job in one ZIP. A [JobReportFailureCode.notFound]
  /// means none is left.
  Future<Uint8List> downloadAll(String requestId);
}
