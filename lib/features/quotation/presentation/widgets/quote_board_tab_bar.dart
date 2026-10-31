import 'package:flutter/material.dart';

import '../../../../core/consts/app_colors.dart';
import '../../../../core/consts/app_text_styles.dart';
import '../../../../l10n/app_localizations.dart';
import '../../domain/entities/quote_board_tab.dart';
import 'quote_board_labels.dart';

/// The Quotations board's status switch: Open / Accepted / Rejected as one
/// segmented control, each with its live count. Three equal segments fit the
/// screen without scrolling, so no tab is ever half off the edge.
///
/// "Expiring soon" cuts across the tabs rather than being one, so it lives
/// beside the open value (see `QuoteValueSummary`); while it's on, no
/// segment is selected.
class QuoteBoardTabBar extends StatelessWidget {
  final QuoteBoardTab selected;
  final bool expiringSoonSelected;
  final int Function(QuoteBoardTab tab) countOf;
  final ValueChanged<QuoteBoardTab> onChanged;

  const QuoteBoardTabBar({
    super.key,
    required this.selected,
    required this.expiringSoonSelected,
    required this.countOf,
    required this.onChanged,
  });

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context)!;

    return Padding(
      padding: const EdgeInsets.symmetric(horizontal: 16),
      child: Container(
        padding: const EdgeInsets.all(4),
        decoration: BoxDecoration(
          color: Colors.white,
          borderRadius: BorderRadius.circular(18),
          border: Border.all(color: AppColours.borderSubtle),
        ),
        child: Row(
          children: [
            for (final tab in QuoteBoardTab.values)
              Expanded(
                child: _Segment(
                  label: tab.label(t),
                  count: countOf(tab),
                  selected: !expiringSoonSelected && tab == selected,
                  onTap: () => onChanged(tab),
                ),
              ),
          ],
        ),
      ),
    );
  }
}

class _Segment extends StatelessWidget {
  final String label;
  final int count;
  final bool selected;
  final VoidCallback onTap;

  const _Segment({required this.label, required this.count, required this.selected, required this.onTap});

  @override
  Widget build(BuildContext context) {
    final foreground = selected ? Colors.white : AppColours.ink;
    return Semantics(
      button: true,
      selected: selected,
      child: GestureDetector(
        onTap: onTap,
        behavior: HitTestBehavior.opaque,
        child: AnimatedContainer(
          duration: const Duration(milliseconds: 180),
          curve: Curves.easeOutCubic,
          height: 40,
          padding: const EdgeInsets.symmetric(horizontal: 6),
          alignment: Alignment.center,
          decoration: BoxDecoration(
            color: selected ? AppColours.primaryColor : Colors.transparent,
            borderRadius: BorderRadius.circular(14),
          ),
          child: Row(
            mainAxisSize: MainAxisSize.min,
            children: [
              Flexible(
                child: Text(
                  label,
                  maxLines: 1,
                  overflow: TextOverflow.ellipsis,
                  style: AppTextStyles.caption.copyWith(
                    fontSize: 13,
                    height: 1,
                    fontWeight: FontWeight.w600,
                    color: foreground,
                  ),
                ),
              ),
              if (count > 0) ...[
                const SizedBox(width: 6),
                Container(
                  height: 20,
                  constraints: const BoxConstraints(minWidth: 20),
                  padding: const EdgeInsets.symmetric(horizontal: 6),
                  alignment: Alignment.center,
                  decoration: BoxDecoration(
                    color: selected ? Colors.white.withValues(alpha: 0.2) : AppColours.primaryTint,
                    borderRadius: BorderRadius.circular(999),
                  ),
                  child: Text(
                    '$count',
                    // A fixed line height keeps the pill round instead of the
                    // font's leading stretching it into an oval.
                    style: AppTextStyles.badge.copyWith(
                      fontSize: 11,
                      height: 1,
                      fontWeight: FontWeight.w700,
                      color: selected ? Colors.white : AppColours.primaryDark,
                    ),
                  ),
                ),
              ],
            ],
          ),
        ),
      ),
    );
  }
}
