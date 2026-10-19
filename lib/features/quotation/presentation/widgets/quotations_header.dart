import 'package:flutter/material.dart';

import '../../../../core/consts/app_colors.dart';
import '../../../../core/consts/app_text_styles.dart';
import '../../../../core/consts/svgs.dart';
import '../../../../core/shared/m_search_field.dart';
import '../../../../core/shared/m_svg.dart';
import '../../../../l10n/app_localizations.dart';
import '../../domain/entities/quote_board_tab.dart';
import 'quote_summary_tiles.dart';

/// The Quotations tab's header: one continuous rounded blue panel holding
/// the title row, the four summary tiles, and the search box — matching
/// the mockup, where the tiles read as part of the header rather than
/// the white board underneath.
class QuotationsHeader extends StatelessWidget {
  final TextEditingController searchController;
  final ValueChanged<String> onSearchChanged;
  final int awaitingClient;
  final int clientReplied;
  final int expiringSoon;
  final int acceptedThisMonth;
  final ValueChanged<QuoteBoardTab> onTab;
  final VoidCallback onExpiringSoon;

  const QuotationsHeader({
    super.key,
    required this.searchController,
    required this.onSearchChanged,
    required this.awaitingClient,
    required this.clientReplied,
    required this.expiringSoon,
    required this.acceptedThisMonth,
    required this.onTab,
    required this.onExpiringSoon,
  });

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context)!;

    return Container(
      padding: EdgeInsets.fromLTRB(
        20,
        MediaQuery.paddingOf(context).top + 32,
        20,
        24,
      ),
      decoration: const BoxDecoration(
        color: AppColours.primaryColor,
        borderRadius: BorderRadius.vertical(bottom: Radius.circular(28)),
      ),
      child: Column(
        mainAxisSize: MainAxisSize.min,
        children: [
          Row(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  mainAxisSize: MainAxisSize.min,
                  children: [
                    Text(
                      t.roleCoordinator,
                      style: AppTextStyles.caption.copyWith(
                        color: AppColours.onPrimaryMuted,
                      ),
                    ),
                    Text(
                      t.quotationsTitle,
                      style: AppTextStyles.pageTitle.copyWith(
                        color: Colors.white,
                        fontSize: 24,
                      ),
                    ),
                  ],
                ),
              ),
              const SizedBox(width: 12),
              IconButton(
                // TODO: date-range and client filter dropdowns aren't built yet.
                onPressed: null,
                tooltip: t.filter,
                style: IconButton.styleFrom(
                  fixedSize: const Size.square(44),
                  backgroundColor: Colors.white.withValues(alpha: 0.16),
                  disabledBackgroundColor: Colors.white.withValues(alpha: 0.16),
                ),
                icon: MSvg(name: Svgs.filter, width: 18, height: 18, color: Colors.white),
              ),
            ],
          ),
          const SizedBox(height: 10),
          QuoteSummaryTiles(
            awaitingClient: awaitingClient,
            clientReplied: clientReplied,
            expiringSoon: expiringSoon,
            acceptedThisMonth: acceptedThisMonth,
            onTab: onTab,
            onExpiringSoon: onExpiringSoon,
          ),
          const SizedBox(height: 16),
          ISearchField(
            controller: searchController,
            onChanged: onSearchChanged,
            hintText: t.searchQuotesHint,
          ),
        ],
      ),
    );
  }
}
