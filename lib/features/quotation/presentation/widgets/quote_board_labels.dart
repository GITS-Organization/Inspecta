import '../../../../l10n/app_localizations.dart';
import '../../domain/entities/quote_board_tab.dart';

extension QuoteBoardTabLabel on QuoteBoardTab {
  String label(AppLocalizations t) => switch (this) {
    QuoteBoardTab.open => t.tabOpen,
    QuoteBoardTab.accepted => t.tabAccepted,
    QuoteBoardTab.rejected => t.tabRejected,
  };
}
