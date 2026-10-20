import '../../../../l10n/app_localizations.dart';
import '../../domain/entities/pricing_unit.dart';
import '../../domain/entities/quotation_failure.dart';

extension QuotationFailureMessage on QuotationFailureCode {
  String message(AppLocalizations t) => switch (this) {
    QuotationFailureCode.permissionDenied => t.actionPermissionDenied,
    QuotationFailureCode.network => t.errorNetwork,
    QuotationFailureCode.unknown => t.actionFailed,
  };
}

extension PricingUnitLabel on PricingUnit {
  String label(AppLocalizations t) => switch (this) {
    PricingUnit.perVisit => t.pricingUnitPerVisit,
    PricingUnit.perDay => t.pricingUnitPerDay,
    PricingUnit.perHour => t.pricingUnitPerHour,
    PricingUnit.perItem => t.pricingUnitPerItem,
  };
}
