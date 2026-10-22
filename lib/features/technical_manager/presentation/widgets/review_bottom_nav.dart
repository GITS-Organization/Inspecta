import 'package:flutter/material.dart';

import '../../../../core/widgets/app_bottom_nav.dart';
import '../../../../l10n/app_localizations.dart';

/// The three tabs on the technical manager's shell.
enum TechnicalManagerTab { review, sent, profile }

/// Review / Sent / Profile.
class ReviewBottomNav extends StatelessWidget {
  final TechnicalManagerTab selected;
  final ValueChanged<TechnicalManagerTab> onSelectTab;

  const ReviewBottomNav({super.key, required this.selected, required this.onSelectTab});

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context)!;
    return AppBottomNav(
      selectedIndex: selected.index,
      onSelect: (i) => onSelectTab(TechnicalManagerTab.values[i]),
      items: [
        AppNavItem(icon: Icons.visibility_rounded, label: t.reviewNavReview),
        AppNavItem(icon: Icons.mark_email_read_rounded, label: t.reviewNavSent),
        AppNavItem(icon: Icons.person_rounded, label: t.profile),
      ],
    );
  }
}
