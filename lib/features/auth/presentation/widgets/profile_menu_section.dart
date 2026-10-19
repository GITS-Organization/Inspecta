import 'package:flutter/material.dart';

import '../../../../core/consts/app_colors.dart';
import '../../../../core/consts/app_text_styles.dart';
import '../../../../core/shared/m_card.dart';
import '../../../../core/widgets/language_switcher_widget.dart';
import '../../../../l10n/app_localizations.dart';

/// The Profile screen's "Account" card: Personal info / Change password /
/// Notifications (not built yet — [onComingSoon]) and Language (wired to
/// the real [LanguageSwitcherWidget]).
class ProfileAccountSection extends StatelessWidget {
  final VoidCallback onComingSoon;

  const ProfileAccountSection({super.key, required this.onComingSoon});

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context)!;
    return MCard(
      padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 4),
      child: Column(
        children: [
          const SizedBox(height: 10),
          Align(
            alignment: AlignmentDirectional.centerStart,
            child: Text(t.accountSectionTitle, style: AppTextStyles.cardTitle.copyWith(fontSize: 16)),
          ),
          ProfileMenuRow(
            icon: Icons.person_outline_rounded,
            label: t.personalInfoLabel,
            onTap: onComingSoon,
          ),
          ProfileMenuRow(
            icon: Icons.lock_outline_rounded,
            label: t.changePasswordLabel,
            onTap: onComingSoon,
          ),
          ProfileMenuRow(
            icon: Icons.notifications_outlined,
            label: t.notifications,
            onTap: onComingSoon,
          ),
          ProfileMenuRow(
            icon: Icons.language_rounded,
            label: t.languageLabel,
            trailing: const LanguageSwitcherWidget(),
            isLast: true,
          ),
        ],
      ),
    );
  }
}

/// The Profile screen's "Help & guides" / "Contact admin" card.
class ProfileHelpSection extends StatelessWidget {
  final VoidCallback onComingSoon;

  const ProfileHelpSection({super.key, required this.onComingSoon});

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context)!;
    return MCard(
      padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 4),
      child: Column(
        children: [
          ProfileMenuRow(
            icon: Icons.help_outline_rounded,
            label: t.helpGuidesLabel,
            tint: AppColours.surfaceMuted,
            iconColor: AppColours.inkSecondary,
            onTap: onComingSoon,
          ),
          ProfileMenuRow(
            icon: Icons.mail_outline_rounded,
            label: t.contactAdminLabel,
            tint: AppColours.surfaceMuted,
            iconColor: AppColours.inkSecondary,
            onTap: onComingSoon,
            isLast: true,
          ),
        ],
      ),
    );
  }
}

/// One icon-tile row of a Profile card — public so a role shell can build its
/// own card of rows (the coordinator's Clients / Inspectors / Timesheets)
/// that looks like the Account one beside it.
class ProfileMenuRow extends StatelessWidget {
  final IconData icon;
  final String label;
  final Color tint;
  final Color iconColor;
  final Widget? trailing;
  final VoidCallback? onTap;
  final bool isLast;

  const ProfileMenuRow({
    super.key,
    required this.icon,
    required this.label,
    this.tint = AppColours.primarySoft,
    this.iconColor = AppColours.primaryDark,
    this.trailing,
    this.onTap,
    this.isLast = false,
  });

  @override
  Widget build(BuildContext context) {
    final row = Container(
      constraints: const BoxConstraints(minHeight: 54),
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
            decoration: BoxDecoration(color: tint, borderRadius: BorderRadius.circular(10)),
            child: Icon(icon, size: 18, color: iconColor),
          ),
          const SizedBox(width: 12),
          Expanded(
            child: Text(label, style: AppTextStyles.emphasis.copyWith(fontSize: 14)),
          ),
          if (trailing != null) trailing!,
          if (onTap != null) ...[
            const SizedBox(width: 4),
            const Icon(Icons.chevron_right_rounded, color: AppColours.inkMuted),
          ],
        ],
      ),
    );
    if (onTap == null) return row;
    return InkWell(onTap: onTap, child: row);
  }
}
