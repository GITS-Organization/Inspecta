import 'package:flutter/material.dart';

import '../../../../core/widgets/app_bottom_nav.dart';
import '../../../../l10n/app_localizations.dart';

/// The three tabs on the inspector's shell.
enum InspectorTab { tasks, certificates, profile }

/// Tasks / Certificates / Profile.
class InspectorBottomNav extends StatelessWidget {
  final InspectorTab selected;
  final ValueChanged<InspectorTab> onSelectTab;

  const InspectorBottomNav({super.key, required this.selected, required this.onSelectTab});

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context)!;
    return AppBottomNav(
      selectedIndex: selected.index,
      onSelect: (i) => onSelectTab(InspectorTab.values[i]),
      items: [
        AppNavItem(icon: Icons.task_alt_rounded, label: t.navTasks),
        AppNavItem(icon: Icons.workspace_premium_rounded, label: t.navCertificates),
        AppNavItem(icon: Icons.person_rounded, label: t.profile),
      ],
    );
  }
}
