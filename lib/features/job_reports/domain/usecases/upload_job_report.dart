import '../entities/job_report.dart';
import '../repositories/job_reports_repository.dart';

/// Adds a report file to a job.
class UploadJobReport {
  final JobReportsRepository _repository;

  const UploadJobReport(this._repository);

  Future<JobReport> call(String requestId, JobReportUpload file) => _repository.upload(requestId, file);
}
