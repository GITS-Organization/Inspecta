import 'package:flutter/material.dart';

import '../../../../core/consts/app_colors.dart';
import '../../../../core/consts/app_text_styles.dart';
import '../../../../core/shared/m_card.dart';
import '../../../../l10n/app_localizations.dart';
import '../../domain/entities/user.dart';
import '../../domain/entities/user_role.dart';

/// The card under the "This month" stats that's specific to the signed-in
/// user's role — the inspector's qualifications — or nothing, for a role with
/// no such card (admin, coordinator, technical manager).
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
      UserRole.inspector => _QualificationsCard(user: user, onComingSoon: onComingSoon),
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

/// The inspector's qualifications: what they are certified to inspect, one
/// chip each. The title says what the chips are, so there is no further label.
class _QualificationsCard extends StatelessWidget {
  final AppUser user;
  final VoidCallback onComingSoon;

  const _QualificationsCard({required this.user, required this.onComingSoon});

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context)!;
    return MCard(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          _CardTitle(t.qualifications),
          const SizedBox(height: 10),
          user.qualifications.isEmpty
              ? Text(t.notSet, style: AppTextStyles.caption)
              : Wrap(
                  spacing: 8,
                  runSpacing: 8,
                  children: [
                    for (final q in user.qualifications)
                      Container(
                        padding: const EdgeInsetsDirectional.fromSTEB(10, 6, 12, 6),
                        decoration: BoxDecoration(
                          color: AppColours.primaryTint,
                          borderRadius: BorderRadius.circular(999),
                        ),
                        child: Row(
                          mainAxisSize: MainAxisSize.min,
                          children: [
                            const Icon(
                              Icons.workspace_premium_outlined,
                              size: 15,
                              color: AppColours.primaryDark,
                            ),
                            const SizedBox(width: 6),
                            Flexible(
                              child: Text(
                                q.name,
                                style: AppTextStyles.badge.copyWith(
                                  color: AppColours.primaryDark,
                                ),
                              ),
                            ),
                          ],
                        ),
                      ),
                  ],
                ),
        ],
      ),
    );
  }
}
