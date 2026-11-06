import 'package:flutter/material.dart';
import 'package:flutter_bloc/flutter_bloc.dart';

import '../../../../core/consts/app_colors.dart';
import '../../../../core/consts/app_text_styles.dart';
import '../../../../core/shared/m_back_button.dart';
import '../../../../l10n/app_localizations.dart';
import '../../domain/certificate_progress.dart';
import '../../domain/certificate_template.dart';
import '../bloc/certificate_cubit.dart';

/// The certificate screen's header: template + title, a live autosave
/// indicator, and the "Step N of 5" progress bar (Feature 05).
class CertificateHeader extends StatelessWidget {
  const CertificateHeader({super.key});

  static String _stepTitle(AppLocalizations t, CertificateStep step) => switch (step) {
    CertificateStep.details => t.examinationDetailsTitle,
    CertificateStep.item => t.itemInformationTitle,
    CertificateStep.questions => t.examinationQuestionsTitle,
    CertificateStep.defects => t.defectsTitle,
    CertificateStep.conclusion => t.conclusionTitle,
  };

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context)!;
    return Container(
      color: Colors.white,
      padding: EdgeInsets.fromLTRB(20, MediaQuery.paddingOf(context).top + 12, 20, 16),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        mainAxisSize: MainAxisSize.min,
        children: [
          Row(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              const MBackButton(),
              const SizedBox(width: 12),
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  mainAxisSize: MainAxisSize.min,
                  children: [
                    Text(t.templateLabel(CertificateTemplate.issuer), style: AppTextStyles.caption),
                    Text(
                      t.certificateTitle,
                      overflow: TextOverflow.ellipsis,
                      style: AppTextStyles.cardTitle.copyWith(fontSize: 20),
                    ),
                  ],
                ),
              ),
              BlocBuilder<CertificateCubit, CertificateState>(
                buildWhen: (previous, current) => previous.saveStatus != current.saveStatus,
                builder: (context, state) => _SaveIndicator(status: state.saveStatus),
              ),
            ],
          ),
          const SizedBox(height: 18),
          BlocBuilder<CertificateCubit, CertificateState>(
            buildWhen: (previous, current) => previous.currentStepNumber != current.currentStepNumber,
            builder: (context, state) {
              final percent = (state.currentStepNumber / state.totalSteps * 100).round();
              return Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                mainAxisSize: MainAxisSize.min,
                children: [
                  Row(
                    mainAxisAlignment: MainAxisAlignment.spaceBetween,
                    children: [
                      Expanded(
                        child: Text(
                          '${t.stepOfTotalLabel(state.currentStepNumber, state.totalSteps)} · ${_stepTitle(t, state.currentStep)}',
                          overflow: TextOverflow.ellipsis,
                          style: AppTextStyles.subtitle.copyWith(fontWeight: FontWeight.w700, fontSize: 13),
                        ),
                      ),
                      const SizedBox(width: 8),
                      Text('$percent%', style: AppTextStyles.subtitle.copyWith(fontSize: 13)),
                    ],
                  ),
                  const SizedBox(height: 8),
                  ClipRRect(
                    borderRadius: BorderRadius.circular(3),
                    child: LinearProgressIndicator(
                      value: state.currentStepNumber / state.totalSteps,
                      minHeight: 6,
                      backgroundColor: AppColours.surfaceMuted,
                      valueColor: const AlwaysStoppedAnimation(AppColours.primaryColor),
                    ),
                  ),
                ],
              );
            },
          ),
        ],
      ),
    );
  }
}

class _SaveIndicator extends StatelessWidget {
  final CertificateSaveStatus status;

  const _SaveIndicator({required this.status});

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context)!;
    return switch (status) {
      CertificateSaveStatus.saving => Row(
        mainAxisSize: MainAxisSize.min,
        children: [
          const SizedBox(
            width: 13,
            height: 13,
            child: CircularProgressIndicator(strokeWidth: 2, color: AppColours.inkMuted),
          ),
          const SizedBox(width: 6),
          Text(t.savingLabel, style: AppTextStyles.caption),
        ],
      ),
      CertificateSaveStatus.saved => Row(
        mainAxisSize: MainAxisSize.min,
        children: [
          const Icon(Icons.check_circle, size: 15, color: AppColours.successIcon),
          const SizedBox(width: 4),
          Text(
            t.savedLabel,
            style: AppTextStyles.caption.copyWith(color: AppColours.successIcon, fontWeight: FontWeight.w700),
          ),
        ],
      ),
      CertificateSaveStatus.error => Text(
        t.actionFailed,
        style: AppTextStyles.caption.copyWith(color: AppColours.dangerText, fontWeight: FontWeight.w700),
      ),
      CertificateSaveStatus.idle => const SizedBox.shrink(),
    };
  }
}
