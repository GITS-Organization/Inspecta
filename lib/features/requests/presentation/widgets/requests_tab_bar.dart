import 'package:flutter/material.dart';

import '../../../../core/widgets/app_segmented_filter.dart';
import '../../../../l10n/app_localizations.dart';
import '../../domain/entities/requests_tab.dart';

/// The inbox's New / Maybe toggle: the app's segmented filter across the full
/// width under the search box, with the number of new requests on New.
class RequestsTabBar extends StatelessWidget {
  final RequestsTab selected;

  /// How many new requests are loaded; hidden at 0.
  final int newCount;
  final ValueChanged<RequestsTab> onChanged;

  const RequestsTabBar({
    super.key,
    required this.selected,
    required this.newCount,
    required this.onChanged,
  });

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context)!;
    return AppSegmentedFilter<RequestsTab>(
      margin: EdgeInsets.zero,
      segments: [
        for (final tab in RequestsTab.values)
          AppSegment(
            value: tab,
            label: _label(t, tab),
            count: tab == RequestsTab.newTab ? newCount : 0,
          ),
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
