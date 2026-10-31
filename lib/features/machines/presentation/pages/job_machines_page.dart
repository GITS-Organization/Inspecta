import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_bloc/flutter_bloc.dart';
import 'package:intl/intl.dart';
import 'package:skeletonizer/skeletonizer.dart';

import '../../../../core/consts/app_colors.dart';
import '../../../../core/consts/app_layout.dart';
import '../../../../core/consts/app_text_styles.dart';
import '../../../../core/framework/mtoast.dart';
import '../../../../core/shared/m_card.dart';
import '../../../../core/shared/m_notice.dart';
import '../../../../core/shared/m_primary_button.dart';
import '../../../../core/widgets/empty_widget.dart';
import '../../../../injection.dart';
import '../../../../l10n/app_localizations.dart';
import '../../../../core/widgets/pdf_preview_page.dart';
import '../../../certificate/domain/certificate_number.dart';
import '../../../certificate/presentation/pages/certificate_page.dart';
import '../../../requests/domain/entities/inspection_request.dart';
import '../../../requests/domain/entities/requests_failure.dart';
import '../../../requests/presentation/widgets/requests_labels.dart';
import '../../../technical_manager/presentation/pages/certificate_review_page.dart';
import '../../domain/entities/job_machines.dart';
import '../../domain/entities/machine_status.dart';
import '../../domain/usecases/get_machine_pdf.dart';
import '../bloc/job_machines_cubit.dart';
import '../widgets/machine_widgets.dart';


/// What the skeleton cards are drawn from; only its shape matters.
const _placeholder = MachineSummary(
  requestItemId: 'placeholder',
  name: 'Overhead crane — 10 t',
  quantity: 1,
  status: MachineStatus.draft,
  serialNumber: 'SN-000000',
  checklistTotal: 20,
  checklistAnswered: 8,
);

/// A job's machines: the progress of their certificates, a "Start inspection"
/// button when the inspector may begin, and one card per machine to open its
/// form.
///
/// With [pdfOnly] a machine opens its certificate PDF instead of its form —
/// for certificates that are already approved and sent.
class JobMachinesPage extends StatelessWidget {
  final InspectionRequest request;
  final bool pdfOnly;

  const JobMachinesPage({super.key, required this.request, this.pdfOnly = false});

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context)!;

    return BlocProvider(
      create: (_) => getIt<JobMachinesCubit>(param1: request.id)..start(),
      child: AnnotatedRegion<SystemUiOverlayStyle>(
        value: SystemUiOverlayStyle.dark,
        child: Scaffold(
          backgroundColor: AppColours.background,
          body: Column(
            children: [
              MachinesHeader(
                eyebrow: request.clientName.isEmpty ? request.id : request.clientName,
                title: t.machinesTitle,
              ),
              const SizedBox(height: 12),
              Expanded(child: _Body(request: request, pdfOnly: pdfOnly)),
            ],
          ),
        ),
      ),
    );
  }
}

class _Body extends StatelessWidget {
  final InspectionRequest request;
  final bool pdfOnly;

  const _Body({required this.request, required this.pdfOnly});

  /// The machine's certificate as the API renders it (the stored final file
  /// once approved), in the full-screen preview.
  Future<void> _viewPdf(BuildContext context, MachineSummary machine) {
    final t = AppLocalizations.of(context)!;
    final number = machine.certificateNumber ?? certNumberFor(request);
    return PdfPreviewPage.open(
      context,
      title: t.certificateTitle,
      subtitle: number,
      fileName: '$number.pdf',
      build: () => getIt<GetMachinePdf>()(request.id, machine.requestItemId),
    );
  }

  // Opens the machine's certificate check list — or, for a technical manager
  // who may review it, the review screen. Submitting, approving or returning
  // moves the machine on, so the machines reload once the user is back.
  Future<void> _open(BuildContext context, MachineSummary machine) async {
    final cubit = context.read<JobMachinesCubit>();
    final review = machine.canReview;
    await Navigator.of(context).push(
      MaterialPageRoute<void>(
        builder: (_) => review
            ? CertificateReviewPage(
                request: request,
                requestItemId: machine.requestItemId,
                inspectorName: request.inspectorName,
              )
            : CertificatePage(request: request, requestItemId: machine.requestItemId),
      ),
    );
    cubit.start();
  }

  Future<void> _start(BuildContext context) async {
    final t = AppLocalizations.of(context)!;
    final failure = await context.read<JobMachinesCubit>().startInspection();
    if (failure != null) MToast.showError(message: failure.message(t));
  }

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context)!;

    return BlocBuilder<JobMachinesCubit, JobMachinesState>(
      builder: (context, state) {
        final job = state.machines;
        if (state.status == JobMachinesStatus.loading || job == null) {
          if (state.status == JobMachinesStatus.error) return _ErrorState(failure: state.failure!);
          return const _Skeleton();
        }

        return RefreshIndicator(
          color: AppColours.primaryColor,
          onRefresh: context.read<JobMachinesCubit>().start,
          child: ListView(
            physics: const AlwaysScrollableScrollPhysics(),
            padding: const EdgeInsets.fromLTRB(16, 4, 16, AppLayout.bottomNavClearance),
            children: [
              _Summary(job: job, fallbackLocation: request.location),
              if (job.canStartInspection) ...[
                const SizedBox(height: 12),
                MPrimaryButton(
                  label: t.startInspectionAction,
                  loading: state.isStarting,
                  onPressed: () => _start(context),
                ),
              ],
              const SizedBox(height: 12),
              if (job.machines.isEmpty)
                SizedBox(height: 320, child: EmptyState(title: t.machinesEmpty))
              else
                for (final machine in job.machines) ...[
                  MachineCard(
                    machine: machine,
                    onTap: () => pdfOnly ? _viewPdf(context, machine) : _open(context, machine),
                    onViewPdf: pdfOnly ? () => _viewPdf(context, machine) : null,
                  ),
                  const SizedBox(height: 12),
                ],
            ],
          ),
        );
      },
    );
  }
}

/// Where and when the job is, and how many machines are submitted.
class _Summary extends StatelessWidget {
  final JobMachines job;
  final String fallbackLocation;

  const _Summary({required this.job, required this.fallbackLocation});

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context)!;
    final locale = Localizations.localeOf(context).languageCode;
    final location = job.location.isEmpty ? fallbackLocation : job.location;
    final scheduledAt = job.scheduledAt?.toLocal();
    final progress = job.progress;
    final value = progress.total == 0 ? 0.0 : (progress.submitted / progress.total).clamp(0.0, 1.0);

    return MCard(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        mainAxisSize: MainAxisSize.min,
        children: [
          if (job.clientName.isNotEmpty) Text(job.clientName, style: AppTextStyles.cardTitle),
          if (location.isNotEmpty) ...[
            const SizedBox(height: 4),
            Row(
              children: [
                const Icon(Icons.location_on_outlined, size: 15, color: AppColours.inkMuted),
                const SizedBox(width: 4),
                Expanded(child: Text(location, style: AppTextStyles.caption, overflow: TextOverflow.ellipsis)),
              ],
            ),
          ],
          if (scheduledAt != null) ...[
            const SizedBox(height: 4),
            Row(
              children: [
                const Icon(Icons.event_outlined, size: 15, color: AppColours.inkMuted),
                const SizedBox(width: 4),
                Text(DateFormat('EEE, d MMM · HH:mm', locale).format(scheduledAt), style: AppTextStyles.caption),
              ],
            ),
          ],
          const SizedBox(height: 16),
          ClipRRect(
            borderRadius: BorderRadius.circular(999),
            child: LinearProgressIndicator(
              value: value,
              minHeight: 8,
              color: AppColours.successIcon,
              backgroundColor: AppColours.primaryTint,
            ),
          ),
          const SizedBox(height: 8),
          Text(t.machinesProgress(progress.submitted, progress.total), style: AppTextStyles.caption),
        ],
      ),
    );
  }
}

/// The loading state: machine cards drawn from [_placeholder], shimmering.
class _Skeleton extends StatelessWidget {
  const _Skeleton();

  @override
  Widget build(BuildContext context) {
    return Skeletonizer(
      child: ListView(
        physics: const NeverScrollableScrollPhysics(),
        padding: const EdgeInsets.fromLTRB(16, 4, 16, 0),
        children: [
          for (var i = 0; i < 3; i++) ...[
            MachineCard(machine: _placeholder, onTap: () {}),
            const SizedBox(height: 12),
          ],
        ],
      ),
    );
  }
}

class _ErrorState extends StatelessWidget {
  final RequestsFailureCode failure;

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
            OutlinedButton(onPressed: context.read<JobMachinesCubit>().retry, child: Text(t.retry)),
          ],
        ),
      ),
    );
  }
}
