import 'package:flutter/material.dart';

import '../../../../core/consts/app_colors.dart';
import '../../../../core/consts/app_text_styles.dart';
import '../../../../core/shared/m_card.dart';
import '../../../../l10n/app_localizations.dart';
import '../../domain/entities/user.dart';
import '../../domain/entities/user_role.dart';

/// The card under the "This month" stats that's specific to the signed-in
/// user's role — or nothing, for a role with no such card (admin, coordinator,
/// technical manager).
class ProfileRoleCard extends StatelessWidget {
  final AppUser user;
  final VoidCallback onComingSoon;

  const ProfileRoleCard({
    super.key,
    required this.user,
    required this.onComingSoon,
  });

  @override
  Widget build(BuildContext context) {
    return switch (user.role) {
      UserRole.coordinator => const SizedBox.shrink(),
      UserRole.inspector => _MyWorkCard(user: user, onComingSoon: onComingSoon),
      UserRole.technicalManager => const SizedBox.shrink(),
      UserRole.admin ||
      UserRole.finance ||
      UserRole.managingDirector ||
      UserRole.merchandiser => const SizedBox.shrink(),
    };
  }
}

class _CardTitle extends StatelessWidget {
  final String title;

  const _CardTitle(this.title);

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.only(bottom: 6),
      child: Text(title, style: AppTextStyles.sectionTitle),
    );
  }
}

class _MyWorkCard extends StatelessWidget {
  final AppUser user;
  final VoidCallback onComingSoon;

  const _MyWorkCard({required this.user, required this.onComingSoon});

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context)!;
    return MCard(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          _CardTitle(t.myWorkTitle),
          const SizedBox(height: 10),
          Text(
            t.qualifiedForLabel,
            style: AppTextStyles.subtitle.copyWith(fontWeight: FontWeight.w600),
          ),
          const SizedBox(height: 8),
          user.qualifications.isEmpty
              ? Text(t.notSet, style: AppTextStyles.caption)
              : Wrap(
                  spacing: 8,
                  runSpacing: 8,
                  children: [
                    for (final q in user.qualifications)
                      Container(
                        padding: const EdgeInsets.symmetric(
                          horizontal: 12,
                          vertical: 6,
                        ),
                        decoration: BoxDecoration(
                          color: AppColours.primaryTint,
                          borderRadius: BorderRadius.circular(999),
                        ),
                        child: Text(
                          q.name,
                          style: AppTextStyles.badge.copyWith(
                            color: AppColours.primaryDark,
                          ),
                        ),
                      ),
                  ],
                ),
          const SizedBox(height: 12),
        ],
      ),
    );
  }
}
