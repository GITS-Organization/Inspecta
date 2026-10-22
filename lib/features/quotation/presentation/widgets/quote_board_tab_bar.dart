import 'package:flutter/material.dart';

import '../../../../core/widgets/app_filter_chip.dart';
import '../../../../l10n/app_localizations.dart';
import '../../domain/entities/quote_board_tab.dart';
import 'quote_board_labels.dart';

/// The Quotations board's one filter row: Replied / Open / Expiring soon /
/// Accepted / Lost, as scrollable chips with a live count on each.
///
/// "Expiring soon" cuts across the tabs rather than being one, so it's a
/// chip of its own; picking it (or any tab) clears the other. Replied is
/// the coordinator's to-do list, so its count is red when there is one.
class QuoteBoardTabBar extends StatelessWidget {
  final QuoteBoardTab selected;
  final bool expiringSoonSelected;
  final int expiringSoonCount;
  final int Function(QuoteBoardTab tab) countOf;
  final ValueChanged<QuoteBoardTab> onChanged;
  final VoidCallback onExpiringSoon;

  const QuoteBoardTabBar({
    super.key,
    required this.selected,
    required this.expiringSoonSelected,
    required this.expiringSoonCount,
    required this.countOf,
    required this.onChanged,
    required this.onExpiringSoon,
  });

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context)!;

    Widget tabChip(QuoteBoardTab tab) => AppFilterChip(
      label: tab.label(t),
      count: countOf(tab),
      alert: tab == QuoteBoardTab.replied,
      selected: !expiringSoonSelected && tab == selected,
      onTap: () => onChanged(tab),
    );

    return SizedBox(
      height: 40,
      child: ListView(
        scrollDirection: Axis.horizontal,
        padding: const EdgeInsets.symmetric(horizontal: 16),
        children: [
          tabChip(QuoteBoardTab.replied),
          const SizedBox(width: 8),
          tabChip(QuoteBoardTab.open),
          const SizedBox(width: 8),
          AppFilterChip(
            label: t.tileExpiringSoon,
            count: expiringSoonCount,
            warning: true,
            selected: expiringSoonSelected,
            onTap: onExpiringSoon,
          ),
          const SizedBox(width: 8),
          tabChip(QuoteBoardTab.accepted),
          const SizedBox(width: 8),
          tabChip(QuoteBoardTab.lost),
        ],
      ),
    );
  }
}
