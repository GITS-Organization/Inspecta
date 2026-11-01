import 'package:flutter/material.dart';

import '../../../../core/consts/app_colors.dart';
import '../../../../core/consts/app_text_styles.dart';
import '../../../../core/utils/currency.dart';
import '../../../../core/widgets/app_filter_chip.dart';
import '../../../../l10n/app_localizations.dart';

/// The row between the status tabs and the list: the board's total open
/// value on the left — one amount per currency, since amounts in different
/// currencies are never added — and on the right the "Expiring soon" toggle,
/// which cuts across the tabs, so it isn't one of them. The toggle is there
/// only while something is expiring (or the filter is on, so it can be
/// cleared).
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
      padding: const EdgeInsets.fromLTRB(20, 12, 16, 0),
      child: Row(
        children: [
          Expanded(
            child: Text.rich(
              TextSpan(
                style: AppTextStyles.subtitle,
                children: [
                  TextSpan(text: '${t.openValueLabel} '),
                  for (final (index, entry) in openTotals.entries.indexed) ...[
                    if (index > 0) const TextSpan(text: ' · '),
                    TextSpan(
                      text: Currency.format(entry.value, code: entry.key),
                      style: const TextStyle(fontWeight: FontWeight.w800, color: AppColours.ink),
                    ),
                  ],
                ],
              ),
              overflow: TextOverflow.ellipsis,
            ),
          ),
          if (showToggle) ...[
            const SizedBox(width: 12),
            AppFilterChip(
              label: t.tileExpiringSoon,
              count: expiringSoonCount,
              warning: true,
              selected: expiringSoonSelected,
              onTap: onExpiringSoon,
            ),
          ],
        ],
      ),
    );
  }
}
