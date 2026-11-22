import '../../../../l10n/app_localizations.dart';
import '../../domain/entities/job_report.dart';
import '../../domain/entities/job_report_failure.dart';

extension JobReportFailureMessage on JobReportFailureCode {
  String message(AppLocalizations t, {JobReportList? limits}) => switch (this) {
    JobReportFailureCode.permissionDenied => t.reportsErrorForbidden,
    JobReportFailureCode.notTheReviewer => t.reportsErrorNotTheReviewer,
    JobReportFailureCode.badRequest => t.reportsErrorBadRequest,
    JobReportFailureCode.notFound => t.reportsErrorNotFound,
    JobReportFailureCode.locked => t.reportsErrorLocked,
    JobReportFailureCode.limitReached => t.reportsErrorLimit,
    JobReportFailureCode.notApproved => t.reportsErrorNotApproved,
    JobReportFailureCode.unsupportedType => t.reportsErrorType,
    JobReportFailureCode.heic => t.reportsErrorHeic,
    JobReportFailureCode.emptyFile => t.reportsErrorEmpty,
    JobReportFailureCode.fileTooLarge => t.reportsErrorTooLarge(
      (limits?.maxFileBytes ?? JobReportList.defaultMaxFileBytes) ~/ (1024 * 1024),
    ),
    JobReportFailureCode.unreadable => t.reportsErrorUnreadable,
    JobReportFailureCode.network => t.errorNetwork,
    JobReportFailureCode.unknown => t.actionFailed,
  };
}
