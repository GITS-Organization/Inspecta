import 'package:flutter/material.dart';
import 'package:flutter/services.dart';

import '../../../../core/consts/app_colors.dart';
import '../../../../core/consts/app_layout.dart';
import '../../../../core/consts/app_text_styles.dart';
import '../../../../core/shared/m_card.dart';
import '../../../../l10n/app_localizations.dart';
import '../widgets/coordinator_bottom_nav.dart';

/// The More tab: the coordinator's screens that don't fit in the bottom bar —
/// Clients, Inspectors, Timesheets and Profile. Picking one swaps it into
/// the shell in place, like any other tab.
class CoordinatorMorePage extends StatelessWidget {
  final ValueChanged<CoordinatorTab> onSelect;

  const CoordinatorMorePage({super.key, required this.onSelect});

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context)!;
    return AnnotatedRegion<SystemUiOverlayStyle>(
      value: SystemUiOverlayStyle.light,
      child: Scaffold(
        backgroundColor: AppColours.background,
        body: Column(
          children: [
            Container(
              width: double.infinity,
              padding: EdgeInsets.fromLTRB(20, MediaQuery.paddingOf(context).top + 24, 20, 24),
              decoration: const BoxDecoration(
                color: AppColours.primaryColor,
                borderRadius: BorderRadius.vertical(bottom: Radius.circular(28)),
              ),
              child: Text(
                t.navMore,
                style: AppTextStyles.pageTitle.copyWith(color: Colors.white, fontSize: 24),
              ),
            ),
            Expanded(
              child: ListView(
                padding: const EdgeInsets.fromLTRB(20, 18, 20, AppLayout.bottomNavClearance),
                children: [
                  MCard(
                    padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 4),
                    child: Column(
                      children: [
                        _MoreRow(
                          icon: Icons.groups_outlined,
                          label: t.navClients,
                          onTap: () => onSelect(CoordinatorTab.clients),
                        ),
                        _MoreRow(
                          icon: Icons.engineering_outlined,
                          label: t.navInspectors,
                          onTap: () => onSelect(CoordinatorTab.inspectors),
                        ),
                        _MoreRow(
                          icon: Icons.schedule_rounded,
                          label: t.navTimesheets,
                          onTap: () => onSelect(CoordinatorTab.timesheets),
                        ),
                        _MoreRow(
                          icon: Icons.person_outline_rounded,
                          label: t.profile,
                          onTap: () => onSelect(CoordinatorTab.profile),
                          isLast: true,
                        ),
                      ],
                    ),
                  ),
                ],
              ),
            ),
          ],
        ),
      ),
    );
  }
}

class _MoreRow extends StatelessWidget {
  final IconData icon;
  final String label;
  final VoidCallback onTap;
  final bool isLast;

  const _MoreRow({required this.icon, required this.label, required this.onTap, this.isLast = false});

  @override
  Widget build(BuildContext context) {
    return InkWell(
      onTap: onTap,
      child: Container(
        constraints: const BoxConstraints(minHeight: 60),
        decoration: isLast
            ? null
            : const BoxDecoration(
                border: Border(bottom: BorderSide(color: AppColours.surfaceMuted)),
              ),
        child: Row(
          children: [
            Container(
              width: 36,
              height: 36,
              alignment: Alignment.center,
              decoration: BoxDecoration(
                color: AppColours.primarySoft,
                borderRadius: BorderRadius.circular(10),
              ),
              child: Icon(icon, size: 18, color: AppColours.primaryDark),
            ),
            const SizedBox(width: 12),
            Expanded(child: Text(label, style: AppTextStyles.emphasis.copyWith(fontSize: 14))),
            const Icon(Icons.chevron_right_rounded, color: AppColours.inkMuted),
          ],
        ),
      ),
    );
  }
}
