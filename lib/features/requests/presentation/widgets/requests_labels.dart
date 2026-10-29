import '../../../../l10n/app_localizations.dart';
import '../../domain/entities/requests_failure.dart';

extension RequestsFailureMessage on RequestsFailureCode {
  String message(AppLocalizations t) => switch (this) {
    RequestsFailureCode.permissionDenied => t.requestsPermissionDenied,
    RequestsFailureCode.network => t.errorNetwork,
    RequestsFailureCode.conflict => t.requestsConflict,
    RequestsFailureCode.unknown => t.requestsLoadError,
  };
}
