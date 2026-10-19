import 'package:flutter/material.dart';
import 'package:flutter_bloc/flutter_bloc.dart';

import '../../../../l10n/app_localizations.dart';
import '../../../requests/domain/entities/inspection_request.dart';
import '../../domain/certificate_number.dart';
import '../../domain/certificate_progress.dart';
import '../../domain/certificate_template.dart';
import '../bloc/certificate_cubit.dart';
import 'certificate_form_fields.dart';
import 'certificate_section_card.dart';

/// Step 1 of the certificate (Feature 05): the top block of the paper
/// report — who it's for (already known from the request), when the
/// examination happened and what it was done against.
class ExaminationDetailsSection extends StatelessWidget {
  final InspectionRequest request;

  const ExaminationDetailsSection({super.key, required this.request});

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context)!;
    return BlocBuilder<CertificateCubit, CertificateState>(
      buildWhen: (previous, current) =>
          previous.certificate?.isDetailsComplete != current.certificate?.isDetailsComplete ||
          previous.currentStep != current.currentStep,
      builder: (context, state) {
        return CertificateSectionCard(
          stepNumber: 1,
          highlighted: state.currentStep == CertificateStep.details,
          done: state.certificate?.isDetailsComplete ?? false,
          title: t.examinationDetailsTitle,
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              CertificateReadOnlyField(
                label: t.clientAndLocationLabel,
                value: [request.clientName, request.location].where((s) => s.isNotEmpty).join(' · '),
              ),
              certificateFieldGap,
              CertificateReadOnlyField(label: t.certificateNumberLabel, value: certNumberFor(request)),
              certificateFieldGap,
              CertificateTextInput(textKey: CertText.clientRepresentative, label: t.clientRepresentativeLabel),
              certificateFieldGap,
              CertificateDateInput(dateKey: CertDate.examination, label: t.examinationDateLabel),
              certificateFieldGap,
              CertificateDateInput(dateKey: CertDate.nextExamination, label: t.nextExaminationDateLabel),
              certificateFieldGap,
              CertificateTextInput(textKey: CertText.standardOfInspection, label: t.standardOfInspectionLabel),
              certificateFieldGap,
              CertificateTextInput(textKey: CertText.testType, label: t.testTypeLabel),
            ],
          ),
        );
      },
    );
  }
}
