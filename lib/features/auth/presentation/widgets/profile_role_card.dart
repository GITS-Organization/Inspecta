import 'package:flutter/material.dart';
import 'package:flutter_bloc/flutter_bloc.dart';

import '../../../../core/consts/app_colors.dart';
import '../../../../core/consts/app_text_styles.dart';
import '../../../../core/shared/m_card.dart';
import '../../../../injection.dart';
import '../../../../l10n/app_localizations.dart';
import '../../../technical_manager/presentation/bloc/my_signature_cubit.dart';
import '../../../technical_manager/presentation/widgets/signature_edit_sheet.dart';
import '../../../technical_manager/presentation/widgets/signature_pad.dart';
import '../../domain/entities/user.dart';
import '../../domain/entities/user_role.dart';

/// The card under the "This month" stats that's specific to the signed-in
/// user's role — or nothing, for a role with no such card (admin, coordinator).
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
      UserRole.technicalManager => _MySignatureCard(user: user),
      UserRole.admin => const SizedBox.shrink(),
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

/// The technical manager's saved signature — drawn here once, then used on
/// every certificate they approve instead of being asked for each time.
class _MySignatureCard extends StatelessWidget {
  final AppUser user;

  const _MySignatureCard({required this.user});

  @override
  Widget build(BuildContext context) {
    return BlocProvider(
      create: (_) => getIt<MySignatureCubit>()..start(user.id),
      child: const _MySignatureView(),
    );
  }
}

class _MySignatureView extends StatelessWidget {
  const _MySignatureView();

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context)!;
    return BlocBuilder<MySignatureCubit, MySignatureState>(
      builder: (context, state) {
        return MCard(
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Row(
                children: [
                  Expanded(child: _CardTitle(t.mySignatureTitle)),
                  TextButton(
                    onPressed: state.isLoaded
                        ? () => showSignatureEditSheet(context)
                        : null,
                    style: TextButton.styleFrom(
                      backgroundColor: AppColours.primaryTint,
                      foregroundColor: AppColours.primaryDark,
                      textStyle: AppTextStyles.badge,
                      padding: const EdgeInsets.symmetric(horizontal: 14),
                      shape: RoundedRectangleBorder(
                        borderRadius: BorderRadius.circular(999),
                      ),
                    ),
                    child: Text(t.updateAction),
                  ),
                ],
              ),
              const SizedBox(height: 10),
              Container(
                width: double.infinity,
                height: 96,
                alignment: Alignment.center,
                decoration: BoxDecoration(
                  color: AppColours.surfaceMuted,
                  borderRadius: BorderRadius.circular(14),
                  border: Border.all(color: AppColours.border),
                ),
                child: state.hasSignature
                    ? SignaturePreview(strokes: state.strokes!)
                    : Row(
                        mainAxisAlignment: MainAxisAlignment.center,
                        children: [
                          const Icon(
                            Icons.draw_outlined,
                            size: 20,
                            color: AppColours.primaryDark,
                          ),
                          const SizedBox(width: 8),
                          Text(
                            state.isLoaded ? t.noSignatureLabel : '…',
                            style: AppTextStyles.emphasis.copyWith(
                              fontSize: 14,
                              color: AppColours.primaryDark,
                            ),
                          ),
                        ],
                      ),
              ),
              if (state.hasSignature) ...[
                const SizedBox(height: 8),
                Text(t.signatureOnFileLabel, style: AppTextStyles.caption),
              ],
            ],
          ),
        );
      },
    );
  }
}
