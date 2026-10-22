import 'package:flutter/material.dart';

import '../../../../core/consts/app_colors.dart';
import '../../../../core/consts/app_text_styles.dart';
import '../../../../l10n/app_localizations.dart';
import '../../../certificate/domain/certificate_template.dart';
import '../../../certificate/domain/entities/certificate.dart';
import '../../../certificate/domain/entities/checklist_answer.dart';
import '../../../certificate/presentation/widgets/certificate_labels.dart';

/// The three findings a reviewer scans first: the function check, and
/// whether a defect is a danger now or could become one.
class CertificateFindingTiles extends StatelessWidget {
  final Certificate certificate;

  const CertificateFindingTiles({super.key, required this.certificate});

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context)!;
    final (
      checkBackground,
      checkForeground,
    ) = switch (certificate.functionCheck) {
      ChecklistAnswer.pass => (
        AppColours.chipGreenBackground,
        AppColours.chipGreenText,
      ),
      ChecklistAnswer.fail => (
        AppColours.chipRedBackground,
        AppColours.chipRedText,
      ),
      ChecklistAnswer.na || ChecklistAnswer.unanswered => (
        AppColours.surfaceMuted,
        AppColours.inkSecondary,
      ),
    };

    String yesNo(bool? answer) => switch (answer) {
      true => t.yesOption,
      false => t.noOption,
      null => '—',
    };

    // A "yes" is the thing to look at, so it takes the warning colour.
    (Color, Color) danger(
      bool? answer, {
      required Color warnBackground,
      required Color warnForeground,
    }) => switch (answer) {
      true => (warnBackground, warnForeground),
      false => (AppColours.chipGreenBackground, AppColours.chipGreenText),
      null => (AppColours.surfaceMuted, AppColours.inkSecondary),
    };

    final existing = certificate.answerOf(CertQuestion.existingDanger);
    final potential = certificate.answerOf(CertQuestion.futureDanger);
    final (existingBackground, existingForeground) = danger(
      existing,
      warnBackground: AppColours.chipRedBackground,
      warnForeground: AppColours.chipRedText,
    );
    final (potentialBackground, potentialForeground) = danger(
      potential,
      warnBackground: AppColours.chipAmberBackground,
      warnForeground: AppColours.chipAmberText,
    );

    return Row(
      children: [
        Expanded(
          child: _FindingTile(
            value: certificate.functionCheck.label(t),
            label: t.functionCheckLabel,
            background: checkBackground,
            foreground: checkForeground,
          ),
        ),
        const SizedBox(width: 8),
        Expanded(
          child: _FindingTile(
            value: yesNo(existing),
            label: t.existingDangerShortLabel,
            background: existingBackground,
            foreground: existingForeground,
          ),
        ),
        const SizedBox(width: 8),
        Expanded(
          child: _FindingTile(
            value: yesNo(potential),
            label: t.potentialDangerShortLabel,
            background: potentialBackground,
            foreground: potentialForeground,
          ),
        ),
      ],
    );
  }
}

class _FindingTile extends StatelessWidget {
  final String value;
  final String label;
  final Color background;
  final Color foreground;

  const _FindingTile({
    required this.value,
    required this.label,
    required this.background,
    required this.foreground,
  });

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.symmetric(vertical: 14, horizontal: 6),
      decoration: BoxDecoration(
        color: background,
        borderRadius: BorderRadius.circular(14),
      ),
      child: Column(
        mainAxisSize: MainAxisSize.min,
        children: [
          Text(
            value,
            style: TextStyle(
              fontSize: 16,
              fontWeight: FontWeight.w700,
              color: foreground,
            ),
          ),
          const SizedBox(height: 2),
          Text(
            label,
            textAlign: TextAlign.center,
            style: AppTextStyles.caption.copyWith(
              fontWeight: FontWeight.w600,
              color: foreground,
              fontSize: 12,
            ),
          ),
        ],
      ),
    );
  }
}
