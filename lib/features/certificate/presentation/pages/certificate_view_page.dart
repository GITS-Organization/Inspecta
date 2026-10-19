import 'package:flutter/material.dart';
import 'package:intl/intl.dart';

import '../../../../core/consts/app_colors.dart';
import '../../../../core/consts/app_text_styles.dart';
import '../../../../core/shared/loading.dart';
import '../../../../core/shared/m_back_button.dart';
import '../../../../injection.dart';
import '../../../../l10n/app_localizations.dart';
import '../../../requests/domain/entities/inspection_request.dart';
import '../../../requests/presentation/widgets/status_chip.dart';
import '../../domain/certificate_number.dart';
import '../../domain/certificate_template.dart';
import '../../domain/entities/certificate.dart';
import '../../domain/usecases/watch_certificate.dart';
import '../widgets/certificate_labels.dart';
import '../widgets/certificate_section_card.dart';

/// A read-only view of a submitted/approved certificate (Feature 05) —
/// the Certificates list's "View". Unlike [CertificatePage], nothing
/// here is editable: the job has already left `in_progress`, so the
/// backend wouldn't accept a write anyway.
class CertificateViewPage extends StatelessWidget {
  final InspectionRequest request;

  const CertificateViewPage({super.key, required this.request});

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context)!;
    return Scaffold(
      backgroundColor: AppColours.background,
      body: SafeArea(
        child: Column(
          children: [
            _ViewHeader(request: request),
            Expanded(
              child: StreamBuilder<Certificate?>(
                stream: getIt<WatchCertificate>()(request.id),
                builder: (context, snapshot) {
                  if (snapshot.connectionState == ConnectionState.waiting) {
                    return Loading.loader(context);
                  }
                  final certificate = snapshot.data;
                  if (certificate == null) {
                    return Center(child: Text(t.certificateNotFound, style: AppTextStyles.subtitle));
                  }
                  return _ReadOnlyReport(request: request, certificate: certificate);
                },
              ),
            ),
          ],
        ),
      ),
    );
  }
}

class _ViewHeader extends StatelessWidget {
  final InspectionRequest request;

  const _ViewHeader({required this.request});

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context)!;
    return Container(
      color: Colors.white,
      padding: EdgeInsets.fromLTRB(20, MediaQuery.paddingOf(context).top + 12, 20, 16),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          const MBackButton(),
          const SizedBox(width: 12),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              mainAxisSize: MainAxisSize.min,
              children: [
                Text(t.templateLabel(CertificateTemplate.id), style: AppTextStyles.caption),
                Text(
                  t.certificateTitle,
                  overflow: TextOverflow.ellipsis,
                  style: AppTextStyles.cardTitle.copyWith(fontSize: 20),
                ),
              ],
            ),
          ),
          Container(
            padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 6),
            decoration: BoxDecoration(color: request.status.chipBackground, borderRadius: BorderRadius.circular(999)),
            child: Text(request.status.label(t), style: AppTextStyles.badge.copyWith(color: request.status.chipText)),
          ),
        ],
      ),
    );
  }
}

class _ReadOnlyReport extends StatelessWidget {
  final InspectionRequest request;
  final Certificate certificate;

  const _ReadOnlyReport({required this.request, required this.certificate});

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context)!;
    final locale = Localizations.localeOf(context).languageCode;
    final dateFormat = DateFormat('d MMM yyyy', locale);

    String text(String key) => certificate.text(key) ?? '—';
    String date(String key) {
      final value = certificate.dateOf(key);
      return value == null ? '—' : dateFormat.format(value);
    }

    String yesNo(String key) => switch (certificate.answerOf(key)) {
      true => t.yesOption,
      false => t.noOption,
      null => '—',
    };

    final item = request.items.isEmpty ? null : request.items.first;
    final conclusion = certificate.finalResult.label(t);

    return ListView(
      padding: const EdgeInsets.fromLTRB(20, 16, 20, 24),
      children: [
        _ReadOnlySection(
          stepNumber: 1,
          title: t.examinationDetailsTitle,
          rows: [
            (t.clientAndLocationLabel, [request.clientName, request.location].where((s) => s.isNotEmpty).join(' · ')),
            (t.certificateNumberLabel, certNumberFor(request)),
            (t.clientRepresentativeLabel, text(CertText.clientRepresentative)),
            (t.examinationDateLabel, date(CertDate.examination)),
            (t.nextExaminationDateLabel, date(CertDate.nextExamination)),
            (t.standardOfInspectionLabel, text(CertText.standardOfInspection)),
            (t.testTypeLabel, text(CertText.testType)),
          ],
        ),
        const SizedBox(height: 14),
        _ReadOnlySection(
          stepNumber: 2,
          title: t.itemInformationTitle,
          rows: [
            (t.inspectedItemLabel, item?.type ?? request.equipmentTitle),
            (t.manufacturerLabel, text(CertText.manufacturer)),
            (t.modelYearLabel, text(CertText.modelYear)),
            (t.maxWorkingRateLabel, text(CertText.maxWorkingRate)),
            (t.serialNumberLabel, text(CertText.serialNumber)),
            (t.ownerIdLabel, text(CertText.ownerId)),
            (t.functionCheckLabel, certificate.functionCheck.label(t)),
            (t.ndtLabel, text(CertText.ndt)),
          ],
        ),
        const SizedBox(height: 14),
        _ReadOnlySection(
          stepNumber: 3,
          title: t.examinationQuestionsTitle,
          rows: [
            (t.questionFirstExamination, yesNo(CertQuestion.firstExamination)),
            if (certificate.answerOf(CertQuestion.firstExamination) == true)
              (t.questionInstalledCorrectly, yesNo(CertQuestion.installedCorrectly)),
            (t.questionWithin6Months, yesNo(CertQuestion.within6Months)),
            (t.questionWithin12Months, yesNo(CertQuestion.within12Months)),
            (t.questionExaminationScheme, yesNo(CertQuestion.examinationScheme)),
            (t.questionExceptionalCircumstances, yesNo(CertQuestion.exceptionalCircumstances)),
          ],
        ),
        const SizedBox(height: 14),
        _ReadOnlySection(
          stepNumber: 4,
          title: t.defectsTitle,
          rows: [
            (t.defectDescriptionLabel, text(CertText.defectDescription)),
            (t.existingDangerLabel, yesNo(CertQuestion.existingDanger)),
            (t.futureDangerLabel, yesNo(CertQuestion.futureDanger)),
            if (certificate.answerOf(CertQuestion.futureDanger) == true)
              (t.futureDangerByLabel, date(CertDate.futureDangerBy)),
            (t.repairsRequiredLabel, text(CertText.repairsRequired)),
            (t.testsCarriedOutLabel, text(CertText.testsCarriedOut)),
          ],
        ),
        const SizedBox(height: 14),
        _ReadOnlySection(
          stepNumber: 5,
          title: t.conclusionTitle,
          rows: [('', conclusion.isEmpty ? '—' : conclusion), (t.noteLabel, text(CertText.note))],
        ),
      ],
    );
  }
}

/// One section of the report as label/value rows, in the same card shape
/// as the editable form.
class _ReadOnlySection extends StatelessWidget {
  final int stepNumber;
  final String title;
  final List<(String, String)> rows;

  const _ReadOnlySection({required this.stepNumber, required this.title, required this.rows});

  @override
  Widget build(BuildContext context) {
    return CertificateSectionCard(
      stepNumber: stepNumber,
      highlighted: false,
      done: true,
      title: title,
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          for (var i = 0; i < rows.length; i++) ...[
            if (i > 0) const SizedBox(height: 12),
            _ValueRow(label: rows[i].$1, value: rows[i].$2),
          ],
        ],
      ),
    );
  }
}

class _ValueRow extends StatelessWidget {
  final String label;
  final String value;

  const _ValueRow({required this.label, required this.value});

  @override
  Widget build(BuildContext context) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      mainAxisSize: MainAxisSize.min,
      children: [
        if (label.isNotEmpty) ...[Text(label, style: AppTextStyles.caption), const SizedBox(height: 2)],
        Text(value, style: AppTextStyles.cardTitle.copyWith(fontSize: 14)),
      ],
    );
  }
}
