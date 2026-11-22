import '../entities/job_report.dart';
import '../repositories/job_reports_repository.dart';

/// Removes a report file from a job.
class DeleteJobReport {
  final JobReportsRepository _repository;

  const DeleteJobReport(this._repository);

  Future<JobReportList> call(String requestId, String fileId) => _repository.delete(requestId, fileId);
}
