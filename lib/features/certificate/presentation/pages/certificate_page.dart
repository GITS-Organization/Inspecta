import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_bloc/flutter_bloc.dart';
import 'package:skeletonizer/skeletonizer.dart';

import '../../../../core/consts/app_colors.dart';
import '../../../../core/consts/app_text_styles.dart';
import '../../../../core/shared/m_notice.dart';
import '../../../../injection.dart';
import '../../../../l10n/app_localizations.dart';
import '../../../requests/domain/entities/inspection_request.dart';
import '../../domain/entities/certificate_failure.dart';
import '../bloc/certificate_cubit.dart';
import '../widgets/certificate_footer.dart';
import '../widgets/certificate_header.dart';
import '../widgets/certificate_form_fields.dart';
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
          return const _FormSkeleton();
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

/// The loading state, in the shape of the form: the first step's card with its
/// fields, then two more cards, shimmering. Only the shapes matter — Skeletonizer
/// turns every text into a bar.
class _FormSkeleton extends StatelessWidget {
  const _FormSkeleton();

  @override
  Widget build(BuildContext context) {
    return Skeletonizer(
      child: ListView(
        physics: const NeverScrollableScrollPhysics(),
        padding: const EdgeInsets.fromLTRB(20, 16, 20, 24),
        children: const [
          _SkeletonCard(
            title: 'Examination details',
            fields: [
              _SkeletonValue(label: 'Client and location', value: 'Client name · Location of the site'),
              certificateFieldGap,
              _SkeletonValue(label: 'Certificate number', value: 'CERT-2026-0000'),
              certificateFieldGap,
              _SkeletonInput(label: 'Client representative'),
              certificateFieldGap,
              _SkeletonInput(label: 'Examination date'),
              certificateFieldGap,
              _SkeletonInput(label: 'Next examination date'),
            ],
          ),
          SizedBox(height: 14),
          _SkeletonCard(
            title: 'Item information',
            fields: [
              _SkeletonInput(label: 'Manufacturer'),
              certificateFieldGap,
              _SkeletonInput(label: 'Serial number'),
            ],
          ),
          SizedBox(height: 14),
          _SkeletonCard(
            title: 'Examination questions',
            fields: [
              _SkeletonValue(label: 'First examination', value: 'Yes or no'),
              certificateFieldGap,
              _SkeletonValue(label: 'Installed correctly', value: 'Yes or no'),
            ],
          ),
        ],
      ),
    );
  }
}

/// A step's card as [CertificateSectionCard] draws it — white, rounded, a
/// numbered circle and a title — with the fields under it.
class _SkeletonCard extends StatelessWidget {
  final String title;
  final List<Widget> fields;

  const _SkeletonCard({required this.title, required this.fields});

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.all(18),
      decoration: BoxDecoration(
        color: Colors.white,
        borderRadius: BorderRadius.circular(20),
        boxShadow: [
          BoxShadow(color: AppColours.ink.withValues(alpha: 0.05), blurRadius: 16, offset: const Offset(0, 6)),
        ],
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        mainAxisSize: MainAxisSize.min,
        children: [
          Row(
            children: [
              // One grey disc, not the circle's own colour.
              Skeleton.leaf(
                child: Container(
                  width: 28,
                  height: 28,
                  decoration: const BoxDecoration(color: AppColours.surfaceMuted, shape: BoxShape.circle),
                ),
              ),
              const SizedBox(width: 12),
              Expanded(child: Text(title, style: AppTextStyles.cardTitle.copyWith(fontSize: 15))),
            ],
          ),
          const SizedBox(height: 16),
          ...fields,
        ],
      ),
    );
  }
}

/// A read-only field: a small label over its value.
class _SkeletonValue extends StatelessWidget {
  final String label;
  final String value;

  const _SkeletonValue({required this.label, required this.value});

  @override
  Widget build(BuildContext context) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      mainAxisSize: MainAxisSize.min,
      children: [
        Text(label, style: AppTextStyles.caption),
        const SizedBox(height: 2),
        Text(value, style: AppTextStyles.cardTitle.copyWith(fontSize: 14)),
      ],
    );
  }
}

/// An input: its label over a box as tall as the real field.
class _SkeletonInput extends StatelessWidget {
  final String label;

  const _SkeletonInput({required this.label});

  @override
  Widget build(BuildContext context) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      mainAxisSize: MainAxisSize.min,
      children: [
        Text(label, style: AppTextStyles.fieldLabel.copyWith(fontSize: 13)),
        const SizedBox(height: 8),
        Skeleton.leaf(
          child: Container(
            height: 48,
            decoration: BoxDecoration(color: AppColours.surfaceMuted, borderRadius: BorderRadius.circular(12)),
          ),
        ),
      ],
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
