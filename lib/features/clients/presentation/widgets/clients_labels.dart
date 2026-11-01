import '../../../../l10n/app_localizations.dart';
import '../../domain/entities/clients_failure.dart';

extension ClientsFailureMessage on ClientsFailureCode {
  String message(AppLocalizations t) => switch (this) {
    ClientsFailureCode.permissionDenied => t.actionPermissionDenied,
    ClientsFailureCode.network => t.errorNetwork,
    ClientsFailureCode.invalidPattern => t.clientsErrorInvalidPattern,
    ClientsFailureCode.notFound => t.clientsErrorNotFound,
    ClientsFailureCode.duplicate => t.clientsErrorDuplicate,
    ClientsFailureCode.unknown => t.actionFailed,
  };
}
