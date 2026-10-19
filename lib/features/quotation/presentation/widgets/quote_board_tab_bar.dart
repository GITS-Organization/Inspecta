import 'package:flutter/material.dart';

import '../../../../core/consts/app_colors.dart';
import '../../../../core/consts/app_text_styles.dart';
import '../../../../l10n/app_localizations.dart';
import '../../domain/entities/quote_board_tab.dart';
import 'quote_board_labels.dart';

/// The Replied / Open / Accepted / Lost switch: a segmented pill control
/// (tinted track, solid white shadowed pill under the selected tab) —
/// matching [RequestsTabBar]'s pattern rather than plain text tabs.
/// Replied carries a red count badge when it has anything in it —
/// "this is the coordinator's to-do list" (the pasted spec's own words).
class QuoteBoardTabBar extends StatelessWidget {
  final QuoteBoardTab selected;
  final int Function(QuoteBoardTab tab) countOf;
  final ValueChanged<QuoteBoardTab> onChanged;

  const QuoteBoardTabBar({
    super.key,
    required this.selected,
    required this.countOf,
    required this.onChanged,
  });

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.all(4),
      decoration: BoxDecoration(
        color: AppColours.surfaceMuted,
        borderRadius: BorderRadius.circular(14),
      ),
      child: Row(
        children: [
          for (final tab in QuoteBoardTab.values)
            Expanded(
              child: _Segment(
                tab: tab,
                selected: tab == selected,
                badgeCount: tab == QuoteBoardTab.replied ? countOf(tab) : 0,
                onTap: () => onChanged(tab),
              ),
            ),
        ],
      ),
    );
  }
}

class _Segment extends StatelessWidget {
  final QuoteBoardTab tab;
  final bool selected;
  final int badgeCount;
  final VoidCallback onTap;

  const _Segment({
    required this.tab,
    required this.selected,
    required this.badgeCount,
    required this.onTap,
  });

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context)!;
    return Semantics(
      button: true,
      selected: selected,
      child: GestureDetector(
        onTap: onTap,
        behavior: HitTestBehavior.opaque,
        child: Container(
          height: 40,
          alignment: Alignment.center,
          decoration: BoxDecoration(
            // Explicit white, not left to a Material/theme default, so the
            // selected segment always reads as a solid white pill (design).
            color: selected ? Colors.white : Colors.transparent,
            borderRadius: BorderRadius.circular(10),
            boxShadow: selected
                ? [
                    BoxShadow(
                      color: AppColours.ink.withValues(alpha: 0.1),
                      blurRadius: 3,
                    ),
                  ]
                : null,
          ),
          child: Row(
            mainAxisSize: MainAxisSize.min,
            children: [
              Text(
                tab.label(t),
                overflow: TextOverflow.ellipsis,
                style: AppTextStyles.emphasis.copyWith(
                  fontSize: 13,
                  fontWeight: selected ? FontWeight.w700 : FontWeight.w600,
                  color: selected ? AppColours.primaryDark : AppColours.inkSecondary,
                ),
              ),
              if (badgeCount > 0) ...[
                const SizedBox(width: 5),
                Container(
                  height: 18,
                  constraints: const BoxConstraints(minWidth: 18),
                  padding: const EdgeInsets.symmetric(horizontal: 5),
                  alignment: Alignment.center,
                  decoration: const BoxDecoration(
                    color: AppColours.errorIcon,
                    borderRadius: BorderRadius.all(Radius.circular(999)),
                  ),
                  child: Text(
                    '$badgeCount',
                    // Without a fixed line-height the font's default leading
                    // makes the Text taller than the 18px badge, stretching
                    // it into an oval that pokes out of the pill (design).
                    style: AppTextStyles.badge.copyWith(
                      fontSize: 11,
                      color: Colors.white,
                      height: 1,
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
