import 'package:flutter/material.dart';

import '../../../../core/widgets/app_segmented_filter.dart';
import '../../../../l10n/app_localizations.dart';
import '../../domain/entities/requests_tab.dart';

/// The inbox's New / Maybe toggle: the app's segmented filter, hugging its
/// content so it sits beside the search box at the same height.
class RequestsTabBar extends StatelessWidget {
  /// Matches `ISearchField`'s height so the two line up in the header.
  static const double height = AppSegmentedFilter.height;

  final RequestsTab selected;
  final ValueChanged<RequestsTab> onChanged;

  const RequestsTabBar({super.key, required this.selected, required this.onChanged});

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context)!;
    return AppSegmentedFilter<RequestsTab>(
      shrinkWrap: true,
      margin: EdgeInsets.zero,
      segments: [
        for (final tab in RequestsTab.values) AppSegment(value: tab, label: _label(t, tab)),
      ],
      selected: selected,
      onChanged: onChanged,
    );
  }

  String _label(AppLocalizations t, RequestsTab tab) => switch (tab) {
    RequestsTab.newTab => t.tabNew,
    RequestsTab.maybe => t.tabMaybe,
  };
}
