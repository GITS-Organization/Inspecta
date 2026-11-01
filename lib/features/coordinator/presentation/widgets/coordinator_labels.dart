import '../../../../l10n/app_localizations.dart';
import '../../../machines/domain/entities/delivery_failure.dart';
import '../../domain/entities/coordinator_failure.dart';

extension DeliveryFailureMessage on DeliveryFailureCode {
  String message(AppLocalizations t) => switch (this) {
    DeliveryFailureCode.permissionDenied => t.actionPermissionDenied,
    DeliveryFailureCode.network => t.errorNetwork,
    DeliveryFailureCode.invalidAddress => t.deliveryErrorInvalidAddress,
    DeliveryFailureCode.noRecipient => t.deliveryErrorNoRecipient,
    DeliveryFailureCode.mailRefused => t.deliveryErrorMailRefused,
    DeliveryFailureCode.conflict => t.deliveryErrorConflict,
    DeliveryFailureCode.unknown => t.actionFailed,
  };
}

extension CoordinatorFailureMessage on CoordinatorFailureCode {
  String message(AppLocalizations t) => switch (this) {
    CoordinatorFailureCode.permissionDenied => t.actionPermissionDenied,
    CoordinatorFailureCode.network => t.errorNetwork,
    CoordinatorFailureCode.unknown => t.actionFailed,
  };
}

/// "Due tomorrow" / "Due in 4 days" chip text for a date still ahead of
/// now (the coordinator queue's urgency badge) — the inverse of
/// [RelativeDuration], which only looks backwards.
extension DueLabel on DateTime {
  String dueLabel(AppLocalizations t, {DateTime? now}) {
    final today = now ?? DateTime.now();
    final days = DateTime(year, month, day).difference(DateTime(today.year, today.month, today.day)).inDays;
    if (days < 0) return t.overdueByDays(-days);
    if (days == 0) return t.dueToday;
    if (days == 1) return t.dueTomorrow;
    return t.dueInDays(days);
  }
}
