import 'package:flutter/material.dart';

import '../../../../core/widgets/app_segmented_filter.dart';
import '../../../../l10n/app_localizations.dart';
import '../../domain/entities/quote_board_tab.dart';
import 'quote_board_labels.dart';

/// The Quotations board's status switch: Open / Accepted / Rejected, each
/// with its live count.
///
/// "Expiring soon" cuts across the tabs rather than being one, so it lives
/// beside the open value (see `QuoteValueSummary`); while it's on, no
/// segment is selected.
class QuoteBoardTabBar extends StatelessWidget {
  final QuoteBoardTab selected;
  final bool expiringSoonSelected;
  final int Function(QuoteBoardTab tab) countOf;
  final ValueChanged<QuoteBoardTab> onChanged;

  /// Space around the track; none by default, since it sits inside the
  /// header banner, under the search box.
  final EdgeInsetsGeometry margin;

  const QuoteBoardTabBar({
    super.key,
    required this.selected,
    required this.expiringSoonSelected,
    required this.countOf,
    required this.onChanged,
    this.margin = EdgeInsets.zero,
  });

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context)!;
    return AppSegmentedFilter<QuoteBoardTab>(
      margin: margin,
      segments: [
        for (final tab in QuoteBoardTab.values)
          AppSegment(value: tab, label: tab.label(t), count: countOf(tab)),
      ],
      selected: expiringSoonSelected ? null : selected,
      onChanged: onChanged,
    );
  }
}
