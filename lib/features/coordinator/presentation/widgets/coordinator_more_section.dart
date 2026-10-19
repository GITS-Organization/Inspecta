import 'package:flutter/material.dart';

import '../../../../core/shared/m_card.dart';
import '../../../../l10n/app_localizations.dart';
import '../../../auth/presentation/widgets/profile_menu_section.dart';
import 'coordinator_bottom_nav.dart';

/// The coordinator's screens that don't fit in the bottom bar — Clients,
/// Inspectors and Timesheets — as a card of rows. It sits at the top of the
/// More screen, which is the Profile screen; picking one swaps it into the
/// shell in place, like any other tab.
class CoordinatorMoreSection extends StatelessWidget {
  final ValueChanged<CoordinatorTab> onSelect;

  const CoordinatorMoreSection({super.key, required this.onSelect});

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context)!;
    return MCard(
      padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 4),
      child: Column(
        children: [
          ProfileMenuRow(
            icon: Icons.groups_outlined,
            label: t.navClients,
            onTap: () => onSelect(CoordinatorTab.clients),
          ),
          ProfileMenuRow(
            icon: Icons.engineering_outlined,
            label: t.navInspectors,
            onTap: () => onSelect(CoordinatorTab.inspectors),
          ),
          ProfileMenuRow(
            icon: Icons.schedule_rounded,
            label: t.navTimesheets,
            onTap: () => onSelect(CoordinatorTab.timesheets),
            isLast: true,
          ),
        ],
      ),
    );
  }
}
