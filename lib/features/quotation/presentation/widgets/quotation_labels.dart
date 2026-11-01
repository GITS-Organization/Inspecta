import '../../../../l10n/app_localizations.dart';
import '../../domain/entities/quotation_failure.dart';

extension QuotationFailureMessage on QuotationFailureCode {
  String message(AppLocalizations t) => switch (this) {
    QuotationFailureCode.permissionDenied => t.actionPermissionDenied,
    QuotationFailureCode.network => t.errorNetwork,
    QuotationFailureCode.invalidAddress => t.deliveryErrorInvalidAddress,
    QuotationFailureCode.mailRefused => t.deliveryErrorMailRefused,
    QuotationFailureCode.unknown => t.actionFailed,
  };
}
