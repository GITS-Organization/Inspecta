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
/// user's role — or nothing, for a role with no such card ([UserRole.admin]).
class ProfileRoleCard extends StatelessWidget {
  final AppUser user;
  final VoidCallback onComingSoon;

  const ProfileRoleCard({super.key, required this.user, required this.onComingSoon});

  @override
  Widget build(BuildContext context) {
    return switch (user.role) {
      UserRole.coordinator => Column(
        children: [
          _QuotationDefaultsCard(onTap: onComingSoon),
          const SizedBox(height: 14),
          const _MyAreasCard(),
        ],
      ),
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
      child: Text(title, style: AppTextStyles.cardTitle.copyWith(fontSize: 16)),
    );
  }
}

/// A label/value settings row. [emphasized] styles the value as a bold
/// primary-coloured action link (e.g. "View", "Edit") instead of plain text.
class _SettingRow extends StatelessWidget {
  final String label;
  final String value;
  final bool emphasized;
  final VoidCallback? onTap;

  const _SettingRow({required this.label, required this.value, this.emphasized = false, this.onTap});

  @override
  Widget build(BuildContext context) {
    final row = Container(
      constraints: const BoxConstraints(minHeight: 52),
      padding: const EdgeInsets.symmetric(vertical: 8),
      alignment: Alignment.centerLeft,
      child: Row(
        children: [
          Expanded(
            child: Text(label, style: AppTextStyles.emphasis.copyWith(fontSize: 14)),
          ),
          Text(
            value,
            style: emphasized
                ? AppTextStyles.link
                : AppTextStyles.subtitle.copyWith(fontWeight: FontWeight.w600),
          ),
        ],
      ),
    );
    if (onTap == null) return row;
    return InkWell(onTap: onTap, child: row);
  }
}

class _DividedRows extends StatelessWidget {
  final List<Widget> rows;

  const _DividedRows(this.rows);

  @override
  Widget build(BuildContext context) {
    return Column(
      children: [
        for (var i = 0; i < rows.length; i++) ...[
          if (i > 0) const Divider(height: 1, color: AppColours.surfaceMuted),
          rows[i],
        ],
      ],
    );
  }
}

class _QuotationDefaultsCard extends StatelessWidget {
  final VoidCallback onTap;

  const _QuotationDefaultsCard({required this.onTap});

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context)!;
    return MCard(
      padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 4),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          const SizedBox(height: 10),
          _CardTitle(t.quotationDefaultsTitle),
          _DividedRows([
            _SettingRow(label: t.quoteValidForLabel, value: t.validityDaysOption(14)),
            _SettingRow(
              label: t.standardPriceListLabel,
              value: t.viewAction,
              emphasized: true,
              onTap: onTap,
            ),
            _SettingRow(label: t.emailSignatureLabel, value: t.editAction, emphasized: true, onTap: onTap),
          ]),
        ],
      ),
    );
  }
}

class _MyAreasCard extends StatelessWidget {
  const _MyAreasCard();

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context)!;
    return MCard(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          _CardTitle(t.myAreasTitle),
          const SizedBox(height: 4),
          Text(t.areasNotTrackedYet, style: AppTextStyles.caption),
        ],
      ),
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
                        padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 6),
                        decoration: BoxDecoration(
                          color: AppColours.primarySoft,
                          borderRadius: BorderRadius.circular(999),
                        ),
                        child: Text(
                          q.name,
                          style: AppTextStyles.badge.copyWith(color: AppColours.primaryDark),
                        ),
                      ),
                  ],
                ),
          const SizedBox(height: 16),
          _AvailabilityToggle(onChanged: onComingSoon),
          const SizedBox(height: 12),
          SizedBox(
            height: 48,
            width: double.infinity,
            child: OutlinedButton(
              onPressed: onComingSoon,
              style: OutlinedButton.styleFrom(
                foregroundColor: AppColours.primaryDark,
                side: const BorderSide(color: AppColours.primaryTint, width: 1.5),
                textStyle: AppTextStyles.buttonLabel.copyWith(fontSize: 14, color: AppColours.primaryDark),
                shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(14)),
              ),
              child: Text(t.requestLeaveAction),
            ),
          ),
        ],
      ),
    );
  }
}

/// Purely local (not persisted) — there's no per-user availability field
/// on the backend yet, so this reflects the session only.
class _AvailabilityToggle extends StatefulWidget {
  final VoidCallback onChanged;

  const _AvailabilityToggle({required this.onChanged});

  @override
  State<_AvailabilityToggle> createState() => _AvailabilityToggleState();
}

class _AvailabilityToggleState extends State<_AvailabilityToggle> {
  bool _available = true;

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context)!;
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 14),
      constraints: const BoxConstraints(minHeight: 48),
      decoration: BoxDecoration(
        color: AppColours.surfaceMuted,
        borderRadius: BorderRadius.circular(14),
      ),
      child: Row(
        children: [
          Expanded(
            child: Text(
              t.availableForTasksLabel,
              style: AppTextStyles.emphasis.copyWith(fontSize: 14),
            ),
          ),
          Switch(
            value: _available,
            activeTrackColor: AppColours.primaryColor,
            onChanged: (value) {
              setState(() => _available = value);
              widget.onChanged();
            },
          ),
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
                    onPressed: state.isLoaded ? () => showSignatureEditSheet(context) : null,
                    style: TextButton.styleFrom(
                      backgroundColor: AppColours.surfaceMuted,
                      foregroundColor: AppColours.primaryDark,
                      textStyle: AppTextStyles.badge,
                      shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(10)),
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
                  color: AppColours.primarySoft,
                  borderRadius: BorderRadius.circular(14),
                  border: Border.all(color: AppColours.primaryTint, width: 1.5),
                ),
                child: state.hasSignature
                    ? SignaturePreview(strokes: state.strokes!)
                    : Row(
                        mainAxisAlignment: MainAxisAlignment.center,
                        children: [
                          const Icon(Icons.draw_outlined, size: 20, color: AppColours.primaryDark),
                          const SizedBox(width: 8),
                          Text(
                            state.isLoaded ? t.noSignatureLabel : '…',
                            style: AppTextStyles.emphasis.copyWith(fontSize: 14, color: AppColours.primaryDark),
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
