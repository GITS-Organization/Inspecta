import '../entities/job_report.dart';
import '../repositories/job_reports_repository.dart';

/// The job's reports.
class GetJobReports {
  final JobReportsRepository _repository;

  const GetJobReports(this._repository);

  Future<JobReportList> call(String requestId) => _repository.list(requestId);
}
