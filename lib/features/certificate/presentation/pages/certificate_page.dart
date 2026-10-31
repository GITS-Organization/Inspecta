import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_bloc/flutter_bloc.dart';

import '../../../../core/consts/app_colors.dart';
import '../../../../core/shared/loading.dart';
import '../../../../core/shared/m_notice.dart';
import '../../../../injection.dart';
import '../../../../l10n/app_localizations.dart';
import '../../../requests/domain/entities/inspection_request.dart';
import '../../domain/entities/certificate_failure.dart';
import '../bloc/certificate_cubit.dart';
import '../widgets/certificate_footer.dart';
import '../widgets/certificate_header.dart';
import '../widgets/certificate_labels.dart';
import '../widgets/conclusion_section.dart';
import '../widgets/defects_section.dart';
import '../widgets/examination_details_section.dart';
import '../widgets/examination_questions_section.dart';
import '../widgets/item_information_section.dart';
import '../widgets/machine_checklist_section.dart';

/// Feature 05's inspection certificate screen: TÜV's report of thorough
/// examination, filled in section by section — examination details, item
/// information, the yes/no questions, defects and the conclusion —
/// autosaved, then submitted to the technical manager.
class CertificatePage extends StatelessWidget {
  final InspectionRequest request;

  /// The machine of the job this form is open on.
  final String requestItemId;

  const CertificatePage({super.key, required this.request, required this.requestItemId});

  @override
  Widget build(BuildContext context) {
    return BlocProvider(
      create: (_) => getIt<CertificateCubit>(param1: request, param2: requestItemId)..start(),
      child: AnnotatedRegion<SystemUiOverlayStyle>(
        value: SystemUiOverlayStyle.dark,
        child: Scaffold(
          backgroundColor: AppColours.background,
          body: SafeArea(
            child: Column(
              children: [
                const CertificateHeader(),
                Expanded(child: _Body(request: request)),
                const CertificateFooter(),
              ],
            ),
          ),
        ),
      ),
    );
  }
}

class _Body extends StatelessWidget {
  final InspectionRequest request;

  const _Body({required this.request});

  @override
  Widget build(BuildContext context) {
    return BlocBuilder<CertificateCubit, CertificateState>(
      buildWhen: (previous, current) =>
          previous.status != current.status ||
          previous.certificate?.reviewNote != current.certificate?.reviewNote,
      builder: (context, state) {
        if (state.status == CertificateStatus.loading) {
          return Loading.loader(context);
        }
        if (state.status == CertificateStatus.error) {
          return _ErrorState(failure: state.failure!);
        }

        final reviewNote = state.certificate?.reviewNote;
        return ListView(
          padding: const EdgeInsets.fromLTRB(20, 16, 20, 24),
          children: [
            if (reviewNote != null && reviewNote.isNotEmpty) ...[
              _ReviewNoteBanner(note: reviewNote),
              const SizedBox(height: 14),
            ],
            ExaminationDetailsSection(request: request),
            const SizedBox(height: 14),
            ItemInformationSection(request: request),
            const SizedBox(height: 14),
            const ExaminationQuestionsSection(),
            const SizedBox(height: 14),
            const MachineChecklistSection(),
            const MachineChecklistGap(),
            const DefectsSection(),
            const SizedBox(height: 14),
            const ConclusionSection(),
          ],
        );
      },
    );
  }
}

class _ReviewNoteBanner extends StatelessWidget {
  final String note;

  const _ReviewNoteBanner({required this.note});

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context)!;
    return Container(
      padding: const EdgeInsets.all(14),
      decoration: BoxDecoration(
        color: AppColours.chipAmberBackground.withValues(alpha: 0.5),
        border: Border.all(color: AppColours.chipAmberTextStrong.withValues(alpha: 0.3), width: 1.5),
        borderRadius: BorderRadius.circular(16),
      ),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          const Icon(Icons.error_outline_rounded, size: 18, color: AppColours.chipAmberTextStrong),
          const SizedBox(width: 10),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              mainAxisSize: MainAxisSize.min,
              children: [
                Text(
                  t.returnedBadge,
                  style: const TextStyle(
                    fontSize: 13,
                    fontWeight: FontWeight.w800,
                    color: AppColours.chipAmberTextStrong,
                  ),
                ),
                const SizedBox(height: 4),
                Text(note, style: const TextStyle(fontSize: 13, color: AppColours.chipAmberTextStrong)),
              ],
            ),
          ),
        ],
      ),
    );
  }
}

class _ErrorState extends StatelessWidget {
  final CertificateFailureCode failure;

  const _ErrorState({required this.failure});

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context)!;
    return Center(
      child: Padding(
        padding: const EdgeInsets.all(24),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            MNotice(type: MNoticeType.error, message: failure.message(t)),
            const SizedBox(height: 16),
            OutlinedButton(
              onPressed: context.read<CertificateCubit>().retry,
              child: Text(t.retry),
            ),
          ],
        ),
      ),
    );
  }
}
