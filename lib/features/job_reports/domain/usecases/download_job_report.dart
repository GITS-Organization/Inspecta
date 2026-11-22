import 'dart:typed_data';

import '../repositories/job_reports_repository.dart';

/// One report file's bytes.
class DownloadJobReport {
  final JobReportsRepository _repository;

  const DownloadJobReport(this._repository);

  Future<Uint8List> call(String requestId, String fileId) => _repository.download(requestId, fileId);
}
