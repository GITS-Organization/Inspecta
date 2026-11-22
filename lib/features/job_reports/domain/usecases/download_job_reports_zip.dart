import 'dart:typed_data';

import '../repositories/job_reports_repository.dart';

/// Every report of a job in one ZIP.
class DownloadJobReportsZip {
  final JobReportsRepository _repository;

  const DownloadJobReportsZip(this._repository);

  Future<Uint8List> call(String requestId) => _repository.downloadAll(requestId);
}
