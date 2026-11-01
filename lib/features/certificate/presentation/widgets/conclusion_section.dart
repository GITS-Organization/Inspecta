import 'package:flutter/material.dart';
import 'package:flutter_bloc/flutter_bloc.dart';

import '../../../../core/consts/app_colors.dart';
import '../../../../core/consts/app_text_styles.dart';
import '../../../../l10n/app_localizations.dart';
import '../../domain/certificate_progress.dart';
import '../../domain/certificate_template.dart';
import '../../domain/entities/certificate_result.dart';
import '../bloc/certificate_cubit.dart';
import 'certificate_form_fields.dart';
import 'certificate_labels.dart';
import 'certificate_section_card.dart';

/// Step 5 of the certificate (Feature 05): the inspector's verdict (the
/// "Conclusion / remarks" row) and a free-form note.
///
/// "Safe with conditions" is no longer offered; the value stays in
/// [CertificateResult] so certificates already saved with it still read
/// correctly.
class ConclusionSection extends StatelessWidget {
  const ConclusionSection({super.key});

  static final _options = [
    for (final result in CertificateResult.values)
      if (result != CertificateResult.safeWithConditions) result,
  ];

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context)!;
    return BlocBuilder<CertificateCubit, CertificateState>(
      buildWhen: (previous, current) =>
          previous.certificate?.finalResult != current.certificate?.finalResult ||
          previous.currentStep != current.currentStep,
      builder: (context, state) {
        final certificate = state.certificate;
        if (certificate == null) return const SizedBox.shrink();
        final cubit = context.read<CertificateCubit>();

        return CertificateSectionCard(
          stepNumber: 5,
          highlighted: state.currentStep == CertificateStep.conclusion,
          done: certificate.isConclusionComplete,
          title: t.conclusionTitle,
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              for (final (i, result) in _options.indexed) ...[
                if (i > 0) const SizedBox(height: 10),
                _ResultOption(
                  label: result.label(t),
                  selected: certificate.finalResult == result,
                  onTap: () => cubit.setFinalResult(result),
                ),
              ],
              certificateFieldGap,
              CertificateTextInput(textKey: CertText.note, label: t.noteLabel, multiline: true),
            ],
          ),
        );
      },
    );
  }
}

class _ResultOption extends StatelessWidget {
  final String label;
  final bool selected;
  final VoidCallback onTap;

  const _ResultOption({required this.label, required this.selected, required this.onTap});

  @override
  Widget build(BuildContext context) {
    return Material(
      color: Colors.transparent,
      child: InkWell(
        borderRadius: BorderRadius.circular(12),
        onTap: onTap,
        child: Container(
          padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 14),
          decoration: BoxDecoration(
            borderRadius: BorderRadius.circular(12),
            border: Border.all(color: selected ? AppColours.primaryColor : AppColours.border, width: 1.5),
          ),
          child: Row(
            children: [
              Icon(
                selected ? Icons.radio_button_checked_rounded : Icons.radio_button_off_rounded,
                size: 20,
                color: selected ? AppColours.primaryColor : AppColours.inkMuted,
              ),
              const SizedBox(width: 12),
              Expanded(
                child: Text(
                  label,
                  style: AppTextStyles.cardTitle.copyWith(
                    fontSize: 14,
                    color: selected ? AppColours.primaryDark : AppColours.inkBody,
                  ),
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }
}
