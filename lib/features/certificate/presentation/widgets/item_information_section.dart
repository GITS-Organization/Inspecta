import 'package:flutter/material.dart';
import 'package:flutter_bloc/flutter_bloc.dart';

import '../../../../core/consts/app_colors.dart';
import '../../../../core/consts/app_text_styles.dart';
import '../../../../l10n/app_localizations.dart';
import '../../../requests/domain/entities/inspection_request.dart';
import '../../domain/certificate_progress.dart';
import '../../domain/certificate_template.dart';
import '../../domain/entities/checklist_answer.dart';
import '../bloc/certificate_cubit.dart';
import 'certificate_form_fields.dart';
import 'certificate_labels.dart';
import 'certificate_section_card.dart';

/// Step 2 of the certificate (Feature 05): the "Item information / Test
/// results" block — what was inspected (pre-filled from the request, but
/// correctable on site) and its function check.
class ItemInformationSection extends StatelessWidget {
  final InspectionRequest request;

  const ItemInformationSection({super.key, required this.request});

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context)!;
    return BlocBuilder<CertificateCubit, CertificateState>(
      buildWhen: (previous, current) =>
          previous.certificate?.isItemComplete != current.certificate?.isItemComplete ||
          previous.currentStep != current.currentStep,
      builder: (context, state) {
        return CertificateSectionCard(
          stepNumber: 2,
          highlighted: state.currentStep == CertificateStep.item,
          done: state.certificate?.isItemComplete ?? false,
          title: t.itemInformationTitle,
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              CertificateReadOnlyField(
                label: t.inspectedItemLabel,
                value: state.machineMode
                    ? state.inspectedItem
                    : (request.items.isEmpty ? request.equipmentTitle : request.items.first.type),
              ),
              // A machine of the API lists its own fields; the checklist
              // replaces the function check.
              if (state.machineMode) ...[
                for (final field in state.machineFields) ...[
                  certificateFieldGap,
                  CertificateTextInput(textKey: field.key, label: field.label),
                ],
              ] else ...[
                certificateFieldGap,
                CertificateTextInput(textKey: CertText.manufacturer, label: t.manufacturerLabel),
                certificateFieldGap,
                CertificateTextInput(textKey: CertText.modelYear, label: t.modelYearLabel),
                certificateFieldGap,
                CertificateTextInput(textKey: CertText.maxWorkingRate, label: t.maxWorkingRateLabel),
                certificateFieldGap,
                CertificateTextInput(textKey: CertText.serialNumber, label: t.serialNumberLabel),
                certificateFieldGap,
                CertificateTextInput(textKey: CertText.ownerId, label: t.ownerIdLabel),
                certificateFieldGap,
                const _FunctionCheck(),
                certificateFieldGap,
                CertificateTextInput(textKey: CertText.ndt, label: t.ndtLabel, hint: t.naOption),
              ],
            ],
          ),
        );
      },
    );
  }
}

/// The item's function check: a panel with three large result cards — Pass,
/// Fail, N/A — each in its own colour. The chosen one fills in and its
/// result is echoed as a pill beside the title, so the outcome reads at a
/// glance without hunting for the highlighted option.
class _FunctionCheck extends StatelessWidget {
  const _FunctionCheck();

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context)!;
    return BlocSelector<CertificateCubit, CertificateState, ChecklistAnswer>(
      selector: (state) => state.certificate?.functionCheck ?? ChecklistAnswer.unanswered,
      builder: (context, answer) {
        final cubit = context.read<CertificateCubit>();
        final style = _AnswerStyle.of(answer);

        return Container(
          padding: const EdgeInsets.all(14),
          decoration: BoxDecoration(
            color: AppColours.surfaceMuted,
            borderRadius: BorderRadius.circular(18),
          ),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            mainAxisSize: MainAxisSize.min,
            children: [
              Row(
                children: [
                  Container(
                    width: 36,
                    height: 36,
                    decoration: BoxDecoration(color: Colors.white, borderRadius: BorderRadius.circular(12)),
                    child: const Icon(Icons.fact_check_outlined, size: 20, color: AppColours.primaryDark),
                  ),
                  const SizedBox(width: 12),
                  Expanded(child: Text(t.functionCheckLabel, style: AppTextStyles.cardTitle)),
                  if (style != null)
                    AnimatedSwitcher(
                      duration: const Duration(milliseconds: 180),
                      child: Container(
                        key: ValueKey(answer),
                        padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 6),
                        decoration: BoxDecoration(color: style.background, borderRadius: BorderRadius.circular(999)),
                        child: Text(
                          answer.label(t),
                          style: AppTextStyles.badge.copyWith(color: style.foreground),
                        ),
                      ),
                    ),
                ],
              ),
              const SizedBox(height: 12),
              Row(
                children: [
                  for (final option in const [ChecklistAnswer.pass, ChecklistAnswer.fail, ChecklistAnswer.na]) ...[
                    if (option != ChecklistAnswer.pass) const SizedBox(width: 8),
                    Expanded(
                      child: _AnswerCard(
                        answer: option,
                        label: option.label(t),
                        selected: answer == option,
                        onTap: () => cubit.setFunctionCheck(option),
                      ),
                    ),
                  ],
                ],
              ),
            ],
          ),
        );
      },
    );
  }
}

/// The colours and icons for one function-check result.
class _AnswerStyle {
  final Color background;
  final Color foreground;
  final Color border;
  final IconData icon;
  final IconData selectedIcon;

  const _AnswerStyle({
    required this.background,
    required this.foreground,
    required this.border,
    required this.icon,
    required this.selectedIcon,
  });

  /// Null while unanswered.
  static _AnswerStyle? of(ChecklistAnswer answer) => switch (answer) {
    ChecklistAnswer.pass => const _AnswerStyle(
      background: AppColours.successBackground,
      foreground: AppColours.successText,
      border: AppColours.successIcon,
      icon: Icons.check_circle_outline_rounded,
      selectedIcon: Icons.check_circle_rounded,
    ),
    ChecklistAnswer.fail => const _AnswerStyle(
      background: AppColours.errorBackground,
      foreground: AppColours.errorText,
      border: AppColours.errorIcon,
      icon: Icons.cancel_outlined,
      selectedIcon: Icons.cancel_rounded,
    ),
    ChecklistAnswer.na => const _AnswerStyle(
      background: AppColours.borderSubtle,
      foreground: AppColours.ink,
      border: AppColours.inkMuted,
      icon: Icons.remove_circle_outline_rounded,
      selectedIcon: Icons.remove_circle_rounded,
    ),
    ChecklistAnswer.unanswered => null,
  };
}

class _AnswerCard extends StatelessWidget {
  final ChecklistAnswer answer;
  final String label;
  final bool selected;
  final VoidCallback onTap;

  const _AnswerCard({required this.answer, required this.label, required this.selected, required this.onTap});

  @override
  Widget build(BuildContext context) {
    final style = _AnswerStyle.of(answer)!;
    return Semantics(
      button: true,
      selected: selected,
      child: GestureDetector(
        onTap: onTap,
        behavior: HitTestBehavior.opaque,
        child: AnimatedContainer(
          duration: const Duration(milliseconds: 180),
          height: 76,
          decoration: BoxDecoration(
            color: selected ? style.background : Colors.white,
            borderRadius: BorderRadius.circular(14),
            border: Border.all(color: selected ? style.border : AppColours.border, width: selected ? 2 : 1),
          ),
          child: Column(
            mainAxisAlignment: MainAxisAlignment.center,
            children: [
              Icon(
                selected ? style.selectedIcon : style.icon,
                size: 26,
                color: selected ? style.border : AppColours.inkMuted,
              ),
              const SizedBox(height: 6),
              Text(
                label,
                style: AppTextStyles.caption.copyWith(
                  fontSize: 13,
                  height: 1,
                  fontWeight: FontWeight.w700,
                  color: selected ? style.foreground : AppColours.ink,
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }
}
