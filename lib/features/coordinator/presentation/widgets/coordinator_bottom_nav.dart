import 'package:flutter/material.dart';

import '../../../../core/widgets/app_bottom_nav.dart';
import '../../../../l10n/app_localizations.dart';

/// Every screen the coordinator's shell swaps in place: Requests, Quotations,
/// Queue and More, which is the Profile screen.
enum CoordinatorTab { requests, quotations, queue, more }

/// Requests / Quotations / Queue / More.
class CoordinatorBottomNav extends StatelessWidget {
  final CoordinatorTab selected;
  final ValueChanged<CoordinatorTab> onSelectTab;

  const CoordinatorBottomNav({super.key, required this.selected, required this.onSelectTab});

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context)!;
    return AppBottomNav(
      selectedIndex: selected.index,
      onSelect: (i) => onSelectTab(CoordinatorTab.values[i]),
      items: [
        AppNavItem(icon: Icons.inbox_rounded, label: t.navRequests),
        AppNavItem(icon: Icons.request_quote_rounded, label: t.navQuotations),
        AppNavItem(icon: Icons.pending_actions_rounded, label: t.navQueue),
        AppNavItem(icon: Icons.more_horiz_rounded, label: t.navMore),
      ],
    );
  }
}
