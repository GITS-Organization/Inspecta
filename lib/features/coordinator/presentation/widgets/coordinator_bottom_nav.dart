import 'package:flutter/material.dart';

import '../../../../core/consts/app_colors.dart';
import '../../../../core/consts/app_text_styles.dart';
import '../../../../l10n/app_localizations.dart';

/// Every screen the coordinator's shell swaps in place. The bar only has
/// room for four of them — Requests, Quotations, Queue and More — so
/// Clients, Inspectors and Timesheets live behind More. More itself is the
/// Profile screen.
enum CoordinatorTab {
  requests,
  quotations,
  queue,
  more,
  clients,
  inspectors,
  timesheets;

  /// Whether this tab has its own slot in the bar. The rest show under More.
  bool get inBar => index <= more.index;
}

/// Requests / Quotations / Queue / More. While one of the screens behind
/// More is showing, More stays the highlighted slot.
class CoordinatorBottomNav extends StatelessWidget {
  final CoordinatorTab selected;
  final ValueChanged<CoordinatorTab> onSelectTab;

  const CoordinatorBottomNav({super.key, required this.selected, required this.onSelectTab});

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context)!;
    final highlighted = selected.inBar ? selected : CoordinatorTab.more;
    return SafeArea(
      top: false,
      child: SizedBox(
        height: 76,
        child: Container(
          padding: const EdgeInsets.fromLTRB(12, 8, 12, 8),
          decoration: const BoxDecoration(
            color: Colors.white,
            border: Border(top: BorderSide(color: AppColours.border, width: 1)),
          ),
          child: Row(
            children: [
              Expanded(
                child: _NavItem(
                  icon: Icons.inbox_outlined,
                  label: t.navRequests,
                  selected: highlighted == CoordinatorTab.requests,
                  onTap: () => onSelectTab(CoordinatorTab.requests),
                ),
              ),
              Expanded(
                child: _NavItem(
                  icon: Icons.description_outlined,
                  label: t.navQuotations,
                  selected: highlighted == CoordinatorTab.quotations,
                  onTap: () => onSelectTab(CoordinatorTab.quotations),
                ),
              ),
              Expanded(
                child: _NavItem(
                  icon: Icons.assignment_ind_outlined,
                  label: t.navQueue,
                  selected: highlighted == CoordinatorTab.queue,
                  onTap: () => onSelectTab(CoordinatorTab.queue),
                ),
              ),
              Expanded(
                child: _NavItem(
                  icon: Icons.menu_rounded,
                  label: t.navMore,
                  selected: highlighted == CoordinatorTab.more,
                  onTap: () => onSelectTab(CoordinatorTab.more),
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }
}

class _NavItem extends StatelessWidget {
  final IconData icon;
  final String label;
  final bool selected;
  final VoidCallback onTap;

  const _NavItem({
    required this.icon,
    required this.label,
    required this.selected,
    required this.onTap,
  });

  @override
  Widget build(BuildContext context) {
    final color = selected ? AppColours.primaryDark : AppColours.inkMuted;
    return Material(
      color: Colors.transparent,
      child: InkWell(
        borderRadius: BorderRadius.circular(14),
        onTap: onTap,
        child: Container(
          decoration: BoxDecoration(
            color: selected ? AppColours.primarySoft : null,
            borderRadius: BorderRadius.circular(14),
          ),
          child: Column(
            mainAxisAlignment: MainAxisAlignment.center,
            children: [
              Icon(icon, size: 22, color: color),
              const SizedBox(height: 3),
              Text(
                label,
                maxLines: 1,
                overflow: TextOverflow.ellipsis,
                style: AppTextStyles.caption.copyWith(
                  fontSize: 12,
                  fontWeight: selected ? FontWeight.w700 : FontWeight.w600,
                  color: color,
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }
}
