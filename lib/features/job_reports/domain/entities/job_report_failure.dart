import '../../../../core/error/failure.dart';

enum JobReportFailureCode {
  /// 403 `forbidden`: this user can't do that (a coordinator uploads, an
  /// inspector removes a file they didn't add).
  permissionDenied,

  /// 403 `not_the_reviewer`: only the job's reviewer (or an admin) can remove
  /// this file.
  notTheReviewer,

  /// 400: the file was refused (type, size, content, machine); the API's
  /// readable reason is in [JobReportFailure.detail].
  badRequest,

  /// 404: the job or the file isn't there (or the file has expired).
  notFound,

  /// 409 `reports_locked`: the job is no longer in progress or returned.
  locked,

  /// 409 `report_limit_reached`: 20 files or 30 MB.
  limitReached,

  /// 409 `not_approved`: a coordinator opened the reports before approval.
  notApproved,

  /// Refused here, before anything is sent: the file's type isn't one the API
  /// takes (`.zip`, and anything outside the allowed list).
  unsupportedType,

  /// Refused here: an iPhone HEIC photo, which has to be a JPEG first.
  heic,

  /// Refused here: the file is empty.
  emptyFile,

  /// Refused here: the file is over the per-file limit.
  fileTooLarge,

  /// The picked file couldn't be read.
  unreadable,

  network,
  unknown,
}

class JobReportFailure extends Failure {
  final JobReportFailureCode code;

  /// The API's readable explanation, when it sent one (the 400 answers).
  final String? detail;

  const JobReportFailure(this.code, {this.detail});

  @override
  String toString() => 'JobReportFailure(${code.name})';
}
