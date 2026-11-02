import 'package:flutter/material.dart';

import '../../../../core/consts/app_colors.dart';
import '../../../../core/consts/app_text_styles.dart';
import '../../../../core/utils/currency.dart';
import '../../../../core/widgets/app_filter_chip.dart';
import '../../../../l10n/app_localizations.dart';

/// The summary between the status tabs and the list: the board's total open
/// value as a navy card — one amount per currency, since amounts in
/// different currencies are never added — and, under it on the right, the
/// "Expiring soon" toggle, which cuts across the tabs, so it isn't one of
/// them. The toggle is there only while something is expiring (or the filter
/// is on, so it can be cleared).
class QuoteValueSummary extends StatelessWidget {
  /// Total open value in minor units, by ISO currency code.
  final Map<String, int> openTotals;
  final int expiringSoonCount;
  final bool expiringSoonSelected;
  final VoidCallback onExpiringSoon;

  const QuoteValueSummary({
    super.key,
    required this.openTotals,
    required this.expiringSoonCount,
    required this.expiringSoonSelected,
    required this.onExpiringSoon,
  });

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context)!;
    final showToggle = expiringSoonCount > 0 || expiringSoonSelected;
    if (openTotals.isEmpty && !showToggle) return const SizedBox.shrink();

    return Padding(
      padding: const EdgeInsets.fromLTRB(16, 16, 16, 0),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          if (openTotals.isNotEmpty)
            Container(
              padding: const EdgeInsets.fromLTRB(20, 16, 16, 16),
              decoration: BoxDecoration(
                color: AppColours.navy,
                borderRadius: BorderRadius.circular(24),
              ),
              child: Row(
                children: [
                  Expanded(
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      mainAxisSize: MainAxisSize.min,
                      children: [
                        Text(
                          t.openValueLabel,
                          style: AppTextStyles.subtitle.copyWith(color: AppColours.onNavyMuted),
                        ),
                        const SizedBox(height: 2),
                        // One line per currency.
                        for (final entry in openTotals.entries)
                          Text(
                            Currency.format(entry.value, code: entry.key),
                            maxLines: 1,
                            overflow: TextOverflow.ellipsis,
                            style: AppTextStyles.pageTitle.copyWith(
                              fontSize: 26,
                              height: 32 / 26,
                              letterSpacing: -0.4,
                              color: Colors.white,
                            ),
                          ),
                      ],
                    ),
                  ),
                  const SizedBox(width: 12),
                  Container(
                    width: 52,
                    height: 52,
                    decoration: BoxDecoration(
                      color: Colors.white.withValues(alpha: 0.12),
                      borderRadius: BorderRadius.circular(16),
                    ),
                    child: const Icon(Icons.description_outlined, size: 26, color: Colors.white),
                  ),
                ],
              ),
            ),
          if (showToggle) ...[
            if (openTotals.isNotEmpty) const SizedBox(height: 10),
            Align(
              alignment: AlignmentDirectional.centerEnd,
              child: AppFilterChip(
                label: t.tileExpiringSoon,
                count: expiringSoonCount,
                warning: true,
                selected: expiringSoonSelected,
                onTap: onExpiringSoon,
              ),
            ),
          ],
        ],
      ),
    );
  }
}
