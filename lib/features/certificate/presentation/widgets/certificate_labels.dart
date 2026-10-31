import '../../../../l10n/app_localizations.dart';
import '../../domain/entities/certificate_failure.dart';
import '../../domain/entities/certificate_result.dart';
import '../../domain/entities/checklist_answer.dart';

extension CertificateFailureMessage on CertificateFailureCode {
  String message(AppLocalizations t) => switch (this) {
    CertificateFailureCode.permissionDenied => t.actionPermissionDenied,
    CertificateFailureCode.network => t.errorNetwork,
    CertificateFailureCode.incomplete => t.machineMissingLabel,
    CertificateFailureCode.unknown => t.actionFailed,
  };
}

extension CertificateResultLabel on CertificateResult? {
  String label(AppLocalizations t) => switch (this) {
    CertificateResult.safeToOperate => t.safeToOperateOption,
    CertificateResult.safeWithConditions => t.safeWithConditionsOption,
    CertificateResult.notSafe => t.notSafeOption,
    null => '',
  };
}

extension ChecklistAnswerLabel on ChecklistAnswer {
  String label(AppLocalizations t) => switch (this) {
    ChecklistAnswer.pass => t.passOption,
    ChecklistAnswer.fail => t.failOption,
    ChecklistAnswer.na => t.naOption,
    ChecklistAnswer.unanswered => '—',
  };
}
