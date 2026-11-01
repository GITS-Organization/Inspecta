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

/// The message for a failure of saving or changing a request: the machine one
/// says what to fix, anything unclassified is a plain "went wrong" (the code's
/// own message talks about loading).
extension RequestsFailureText on RequestsFailure {
  String text(AppLocalizations t) => switch (this) {
    InvalidMachineFailure() => t.invalidMachineError,
    _ when code == RequestsFailureCode.unknown => t.actionFailed,
    _ => code.message(t),
  };
}
